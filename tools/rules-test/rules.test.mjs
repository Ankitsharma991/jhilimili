// Phase 10 step 2: Docs/Phase10/firebase/firestore.rules and storage.rules on the emulators. Mirrors CloudModelTests
// (the memory cloud); when a rule changes, both change. Run: cd tools/rules-test && npm test
import { readFileSync } from 'node:fs';
import { test, before, after, beforeEach } from 'node:test';
import { initializeTestEnvironment, assertSucceeds, assertFails } from '@firebase/rules-unit-testing';
import firebase from 'firebase/compat/app';

const RULES = '../../Docs/Phase10/firebase/';
const KEY = 'A'.repeat(43) + '=';
const CODE = 'BCDF2345';
let env;

const google = (uid) => env.authenticatedContext(uid, { firebase: { sign_in_provider: 'google.com' } });
const anon = (uid) => env.authenticatedContext(uid, { firebase: { sign_in_provider: 'anonymous' } });
const now = () => Math.floor(Date.now() / 1000);
const day = () => Math.floor(Date.now() / 86400000);
const inAYear = () => firebase.firestore.Timestamp.fromMillis(Date.now() + 365 * 86400000);

before(async () => {
  env = await initializeTestEnvironment({
    projectId: 'demo-utsav',
    firestore: { rules: readFileSync(RULES + 'firestore.rules', 'utf8'), host: '127.0.0.1', port: 8080 },
    storage: { rules: readFileSync(RULES + 'storage.rules', 'utf8'), host: '127.0.0.1', port: 9199 },
  });
});

after(async () => env.cleanup());

beforeEach(async () => {
  await env.clearFirestore();
  await env.withSecurityRulesDisabled(async (ctx) => {
    await ctx.firestore().doc('users/alice').set({ backupKey: KEY, day: 0, sharesToday: 0 });
  });
});

/** Alice publishes CODE the way the game does: counter, share and content in one batch. */
function publish(db, uid, { code = CODE, count = 1, kind = 'home' } = {}) {
  const b = db.batch();
  b.update(db.doc(`users/${uid}`), { day: day(), sharesToday: count });
  b.set(db.doc(`shares/${code}`), {
    kind, owner: uid, createdUnix: now(), lastVisitUnix: now(), visits: 0, reports: 0, withdrawn: false, version: 1, expireAt: inAYear(),
  });
  b.set(db.doc(`shares/${code}/content/main`), { payload: '{"design":{}}' });
  return b.commit();
}

test('accounts: own document only; the key never changes; anonymous accounts get none', async () => {
  await assertSucceeds(google('alice').firestore().doc('users/alice').get());
  await assertFails(google('bob').firestore().doc('users/alice').get());
  await assertFails(google('alice').firestore().doc('users/alice').update({ backupKey: 'B'.repeat(43) + '=' }));
  await assertSucceeds(google('bob').firestore().doc('users/bob').set({ backupKey: KEY, day: 0, sharesToday: 0 }));
  await assertFails(anon('carol').firestore().doc('users/carol').set({ backupKey: KEY, day: 0, sharesToday: 0 }));
  await assertFails(google('dave').firestore().doc('users/dave').set({ backupKey: 'short', day: 0, sharesToday: 0 }));
});

test('the share counter only moves by publishing', async () => {
  const db = google('alice').firestore();
  await assertFails(db.doc('users/alice').update({ day: day(), sharesToday: 0 }), 'no reset to zero');
  await assertFails(db.doc('users/alice').update({ day: day(), sharesToday: 5 }), 'no jump');
  await assertSucceeds(db.doc('users/alice').update({ day: day(), sharesToday: 1 }), 'a new day starts at one');
  await assertFails(db.doc('users/alice').update({ day: day(), sharesToday: 1 }), 'the same day only goes up');
});

test('publishing: a new code with content and the counter, within the day\'s limit', async () => {
  const db = google('alice').firestore();
  await assertSucceeds(publish(db, 'alice'));
  await assertFails(publish(db, 'alice', { count: 2 }), 'a taken code');
  const lone = db.batch();
  lone.set(db.doc('shares/CDFG3456'), { kind: 'home', owner: 'alice', createdUnix: now(), lastVisitUnix: now(), visits: 0, reports: 0, withdrawn: false, version: 1, expireAt: inAYear() });
  lone.set(db.doc('shares/CDFG3456/content/main'), { payload: '{}' });
  await assertFails(lone.commit(), 'without raising the counter');
  await assertFails(publish(db, 'alice', { code: 'CDFG345A', count: 2 }), 'not a code (vowel)');
  await assertFails(publish(google('bob').firestore(), 'alice', { code: 'CDFG3456', count: 2 }), 'for someone else');
  await env.withSecurityRulesDisabled((ctx) => ctx.firestore().doc('users/alice').update({ day: day(), sharesToday: 10 }));
  await assertFails(publish(db, 'alice', { code: 'DFGH4567', count: 11 }), 'past ten a day');
  await assertFails(publish(anon('carol').firestore(), 'carol', { code: 'FGHJ5678' }), 'anonymous accounts can\'t publish');
});

test('visiting: open by code, never list; content only while visible', async () => {
  await publish(google('alice').firestore(), 'alice');
  const visitor = anon('carol').firestore();
  await assertSucceeds(visitor.doc(`shares/${CODE}`).get());
  await assertSucceeds(visitor.doc(`shares/${CODE}/content/main`).get());
  await assertSucceeds(visitor.doc('shares/ZZZZZZZZ').get(), 'a missing code reads as missing, not denied');
  await assertFails(visitor.collection('shares').get(), 'no paging through codes');
  await assertSucceeds(google('alice').firestore().collection('shares').where('owner', '==', 'alice').get(), 'the owner lists their own');
  await assertFails(env.unauthenticatedContext().firestore().doc(`shares/${CODE}`).get(), 'signed in (anonymously at least)');
});

test('a visit raises visits by one, by someone else, and nothing more', async () => {
  await publish(google('alice').firestore(), 'alice');
  const visit = (db, by = 1, extra = {}) => db.doc(`shares/${CODE}`).update({
    visits: firebase.firestore.FieldValue.increment(by), lastVisitUnix: now(), expireAt: inAYear(), ...extra,
  });
  await assertSucceeds(visit(anon('carol').firestore()));
  await assertFails(visit(anon('carol').firestore(), 2), 'by two');
  await assertFails(visit(google('alice').firestore()), 'the owner\'s own visit');
  await assertFails(visit(anon('carol').firestore(), 1, { reports: 0 }), 'touching reports');
  await assertFails(anon('carol').firestore().doc(`shares/${CODE}`).update({ kind: 'show' }));
});

test('reports: once per account, with a reason from the list; three hide the content', async () => {
  await publish(google('alice').firestore(), 'alice');
  const report = (uid, reason = 'offensive') => {
    const db = anon(uid).firestore();
    const b = db.batch();
    b.set(db.doc(`shares/${CODE}/reports/${uid}`), { reason, unix: now() });
    b.update(db.doc(`shares/${CODE}`), { reports: firebase.firestore.FieldValue.increment(1) });
    return b.commit();
  };
  await assertSucceeds(report('r1'));
  await assertFails(report('r1'), 'the same account twice');
  await assertFails(report('r2', 'I just do not like it'), 'free text');
  await assertFails(anon('r2').firestore().doc(`shares/${CODE}`).update({ reports: firebase.firestore.FieldValue.increment(1) }), 'a count without a report');
  await assertFails(anon('r2').firestore().doc(`shares/${CODE}/reports/r1`).get(), 'reports are private');
  await assertFails(report('alice'), 'the owner can\'t report their own');
  await assertSucceeds(report('r2'));
  await assertSucceeds(report('r3'));
  await assertFails(anon('carol').firestore().doc(`shares/${CODE}/content/main`).get(), 'hidden after three');
  await assertSucceeds(anon('carol').firestore().doc(`shares/${CODE}`).get(), 'but the game can say why');
  await assertSucceeds(google('alice').firestore().doc(`shares/${CODE}/content/main`).get(), 'the owner still sees their own');
});

test('withdrawing and deleting are the owner\'s', async () => {
  await publish(google('alice').firestore(), 'alice');
  await assertFails(google('bob').firestore().doc(`shares/${CODE}`).update({ withdrawn: true }));
  const db = google('alice').firestore();
  const b = db.batch();
  b.update(db.doc(`shares/${CODE}`), { withdrawn: true });
  b.delete(db.doc(`shares/${CODE}/content/main`));
  await assertSucceeds(b.commit());
  await assertFails(anon('carol').firestore().doc(`shares/${CODE}/content/main`).get());
  await assertFails(db.doc(`shares/${CODE}`).update({ withdrawn: false }), 'no coming back');
  await assertFails(google('bob').firestore().doc(`shares/${CODE}`).delete());
  await assertSucceeds(db.doc(`shares/${CODE}`).delete());
});

test('backups: own folder only, sealed bytes under 8 MB, Google accounts', async () => {
  const bytes = new Uint8Array([85, 84, 83, 66, 1, 0]);
  const meta = { contentType: 'application/octet-stream' };
  const name = '001790000000_000.utsav';
  await assertSucceeds(google('alice').storage().ref(`backups/alice/${name}`).put(bytes, meta));
  await assertSucceeds(google('alice').storage().ref(`backups/alice/${name}`).getMetadata());
  await assertFails(google('bob').storage().ref(`backups/alice/${name}`).getMetadata());
  await assertFails(google('bob').storage().ref(`backups/alice/x.utsav`).put(bytes, meta));
  await assertFails(google('alice').storage().ref('backups/alice/notes.txt').put(bytes, meta), 'only backup names');
  await assertFails(anon('carol').storage().ref(`backups/carol/${name}`).put(bytes, meta));
  await assertFails(google('alice').storage().ref(`elsewhere/${name}`).put(bytes, meta));
  await assertSucceeds(google('alice').firestore().doc('users/alice/backups/001790000000_000').set({ createdUnix: now(), bytes: 6, decorations: 1, shows: 0, avatar: true }));
  await assertFails(google('bob').firestore().doc('users/alice/backups/001790000000_000').get());
});

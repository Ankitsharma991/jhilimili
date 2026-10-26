using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UtsavNights.Content;
using UtsavNights.Core.Crackers;
using UtsavNights.EditorTools.ContentLint;

namespace UtsavNights.Tests
{
    public class StageRepeatTests
    {
        private CrackerDefinition _def;

        [SetUp]
        public void SetUp()
        {
            _def = ScriptableObject.CreateInstance<CrackerDefinition>();
            _def.Id = "cake_test";
            _def.FuseSeconds = 2f;
            _def.EffectVolume = new Vector3(3f, 14f, 3f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_def);

        [Test]
        public void Repeats_ExpandIntoCues_InTimeOrder()
        {
            _def.MutableStages.Add(new StageDefinition { time = 0f });
            _def.MutableStages.Add(new StageDefinition { time = 2f, payoff = true, repeatCount = 4, repeatInterval = 0.5f });
            _def.MutableStages.Add(new StageDefinition { time = 2.3f, repeatCount = 2, repeatInterval = 0.5f });
            _def.MutableStages.Add(new StageDefinition { time = 6f, stop = true, repeatCount = 9 });
            CrackerTimeline t = _def.BuildTimeline();

            Assert.AreEqual(1 + 4 + 2 + 1, t.CueCount, "stop never repeats");
            float last = -1f;
            for (int i = 0; i < t.CueCount; i++)
            {
                Assert.GreaterOrEqual(t[i].Time, last);
                last = t[i].Time;
            }

            var stage1 = new List<TimelineCue>();
            for (int i = 0; i < t.CueCount; i++)
            {
                if (t[i].StageIndex == 1)
                {
                    stage1.Add(t[i]);
                }
            }

            Assert.AreEqual(4, stage1.Count);
            Assert.AreEqual(CueKind.Payoff, stage1[0].Kind, "first repeat enters payoff");
            Assert.AreEqual(CueKind.Normal, stage1[3].Kind, "later repeats are normal cues");
            Assert.AreEqual(3.5f, stage1[3].Time, 1e-5f);
            Assert.AreEqual(3, stage1[3].Repeat);
        }

        [Test]
        public void RepeatsPastStop_MakeTimelineInvalid()
        {
            _def.MutableStages.Add(new StageDefinition { time = 2f, repeatCount = 10, repeatInterval = 1f });
            _def.MutableStages.Add(new StageDefinition { time = 5f, stop = true });
            Assert.IsNotEmpty(_def.ValidateTimeline(), "repeats at 6..11 s run past the stop at 5 s");
        }

        [Test]
        public void DefaultsKeepOldAssetsUnchanged()
        {
            var stage = new StageDefinition();
            Assert.AreEqual(1, stage.Repeats);
            Assert.AreEqual(StageMotion.None, stage.motion);
            Assert.AreEqual(StageAnchor.Base, stage.anchor);
        }

        [Test]
        public void Linter_FlagsMotionAndRepeatMistakes()
        {
            _def.EffectVolume = new Vector3(1f, 3f, 1f);
            _def.MutableStages.Add(new StageDefinition { time = 0f, motion = StageMotion.Launch, launchHeight = 12f });
            _def.MutableStages.Add(new StageDefinition { time = 1f, anchor = StageAnchor.Apex, launchHeight = 0f });
            _def.MutableStages.Add(new StageDefinition { time = 2f, repeatCount = 3, repeatInterval = 0f });
            _def.MutableStages.Add(new StageDefinition { time = 3f, motion = StageMotion.Spin, spinDegreesPerSecond = 0f });
            _def.MutableStages.Add(new StageDefinition { time = 4f, stop = true });
            List<LintIssue> issues = CrackerDefinitionLinter.Lint(_def);
            Assert.IsTrue(issues.Exists(i => i.Rule == "stage.launch_emitter"));
            Assert.IsTrue(issues.Exists(i => i.Rule == "stage.apex_height" && i.Severity == LintSeverity.Error));
            Assert.IsTrue(issues.Exists(i => i.Rule == "stage.repeat_interval"));
            Assert.IsTrue(issues.Exists(i => i.Rule == "stage.spin_zero"));
            Assert.IsTrue(issues.Exists(i => i.Rule == "placement.sky_corridor"));
        }
    }
}

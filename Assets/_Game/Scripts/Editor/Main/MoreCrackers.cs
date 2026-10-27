using System;
using UnityEditor;
using UnityEngine;
using UtsavNights.Content;
using UtsavNights.Core;
using UtsavNights.Core.Crackers;
using F = UtsavNights.EditorTools.Main.FlowerpotAssets;
using Random = System.Random;

namespace UtsavNights.EditorTools.Main
{
    /// <summary>
    /// The remaining R0 crackers (PRD 7): sparkler, chain crackers (lar), single bang, skyshot and multi-shot
    /// cake. Built from code like the others: placeholder synthesised audio (own-003), code-built prefabs and
    /// models (own-004), and definitions. Re-running updates everything in place.
    /// </summary>
    public static class MoreCrackers
    {
        private const string Root = "Assets/_Game/Prefabs/Crackers";
        private const string Defs = "Assets/_Game/Definitions/Crackers";

        public sealed class Set
        {
            public CrackerDefinition Sparkler;
            public CrackerDefinition Lar;
            public CrackerDefinition Bomb;
            public CrackerDefinition Skyshot;
            public CrackerDefinition Cake;
        }

        public static Set BuildAll()
        {
            Texture2D glow = AssetDatabase.LoadAssetAtPath<Texture2D>(F.GlowTexturePath);
            Material add = F.ParticleMaterial("More_Spark_Additive", glow, additive: true);
            Material alpha = F.ParticleMaterial("More_Smoke_Alpha", glow, additive: false);
            ParticleSystem fuseSpark = AssetDatabase.LoadAssetAtPath<ParticleSystem>(Root + "/Flowerpot/FX_Flowerpot_FuseSpark.prefab");
            AudioClip fuseHiss = AssetDatabase.LoadAssetAtPath<AudioClip>($"{F.AudioFolder}/fuse_hiss_loop.wav");

            return new Set
            {
                Sparkler = Sparkler(add, fuseSpark, fuseHiss),
                Lar = Lar(add, alpha, fuseSpark, fuseHiss),
                Bomb = Bomb(add, alpha, fuseSpark, fuseHiss),
                Skyshot = Skyshot(add, fuseSpark, fuseHiss),
                Cake = Cake(add, fuseSpark, fuseHiss),
            };
        }

        // ---------------------------------------------------------------- Sparkler

        private static CrackerDefinition Sparkler(Material add, ParticleSystem fuseSpark, AudioClip fuseHiss)
        {
            const string folder = Root + "/Sparkler";
            AudioClip sizzle = F.WriteClip("sparkler_sizzle_loop", Sizzle(F.SampleRate, 6101), true);

            ParticleSystem stars = F.NewSystem("FX_Sparkler_Stars", add, 500, true);
            Main(stars, 0.3f, 0.75f, 1.6f, 3.6f, 0.012f, 0.03f, 0.55f, Color.white, new Color(1f, 0.9f, 0.55f));
            Emit(stars, 420f);
            Sphere(stars, 0.012f);
            var noise = stars.noise;
            noise.enabled = true;
            noise.strength = 0.8f;
            noise.frequency = 3f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            F.Fade(stars, Color.white, new Color(1f, 0.75f, 0.35f), 0.3f);
            stars = F.SavePrefab(stars, "FX_Sparkler_Stars", folder);

            ParticleSystem core = F.NewSystem("FX_Sparkler_Core", add, 30, false);
            Main(core, 0.06f, 0.12f, 0f, 0.05f, 0.1f, 0.16f, 0f, new Color(1f, 1f, 0.9f), new Color(1f, 0.85f, 0.5f));
            Emit(core, 160f);
            Sphere(core, 0.004f);
            core = F.SavePrefab(core, "FX_Sparkler_Core", folder);

            var model = new GameObject("Model_Sparkler");
            Material wire = SceneKit.LitMaterial("Sparkler_Wire", new Color(0.55f, 0.55f, 0.58f), 0.6f);
            Material coat = SceneKit.LitMaterial("Sparkler_Coating", new Color(0.32f, 0.3f, 0.3f), 0.2f);
            Material sand = SceneKit.LitMaterial("Sparkler_Sand", new Color(0.55f, 0.42f, 0.28f), 0.05f);
            SceneKit.Primitive(PrimitiveType.Sphere, "Mound", model.transform, new Vector3(0f, 0.01f, 0f), new Vector3(0.12f, 0.04f, 0.12f), sand, false);
            SceneKit.Primitive(PrimitiveType.Cylinder, "Handle", model.transform, new Vector3(0f, 0.14f, 0f), new Vector3(0.006f, 0.13f, 0.006f), wire, false);
            SceneKit.Primitive(PrimitiveType.Cylinder, "Coating", model.transform, new Vector3(0f, 0.4f, 0f), new Vector3(0.014f, 0.13f, 0.014f), coat, false);
            GameObject modelPrefab = F.SaveGameObject(model, "Model_Sparkler", folder);

            CrackerDefinition d = Load($"{Defs}/Cracker_Sparkler.asset");
            Common(d, "sparkler_classic", new LocalizedName("Sparkler", "Phuljhadi", "Phuljhadi"), CrackerCategory.Sparkler, Intensity.Gentle, 1, modelPrefab);
            d.Footprint = new Vector2(0.15f, 0.15f);
            d.EffectVolume = new Vector3(0.6f, 1.2f, 0.6f);
            d.Surfaces = PlacementSurface.Ground | PlacementSurface.Hand;
            d.FuseSeconds = 1.2f;
            d.FuseAnchor = new Vector3(0f, 0.53f, 0f);
            d.CooldownSeconds = 1.5f;
            d.Wavable = true;
            Quality(d, 200, 350, 500);
            var s = d.MutableStages;
            s.Clear();
            s.Add(new StageDefinition { time = 0f, emitter = fuseSpark, anchor = StageAnchor.Fuse, endWithNextEmitter = true, sound = fuseHiss, loopSound = true, volume = 0.35f, haptic = HapticPattern.Tick });
            s.Add(new StageDefinition { time = 1.2f, emitter = stars, anchor = StageAnchor.Fuse, motion = StageMotion.Follow, payoff = true, sound = sizzle, loopSound = true, volume = 0.6f, haptic = HapticPattern.Tick });
            s.Add(new StageDefinition { time = 1.2f, emitter = core, anchor = StageAnchor.Fuse, motion = StageMotion.Follow });
            s.Add(new StageDefinition { time = 13f, stop = true });
            return Save(d);
        }

        // ---------------------------------------------------------------- Chain crackers (lar)

        private static CrackerDefinition Lar(Material add, Material alpha, ParticleSystem fuseSpark, AudioClip fuseHiss)
        {
            const string folder = Root + "/Lar";
            const int pops = 40;
            const float spacing = 0.03f;
            const float interval = 0.075f;
            AudioClip pop = F.WriteClip("lar_pop", ProceduralSounds.Generate(ProceduralSounds.Kind.Pop, F.SampleRate, 6201), false);

            ParticleSystem flash = F.NewSystem("FX_Lar_Pop", add, 26, false);
            Main(flash, 0.06f, 0.16f, 0.5f, 2.5f, 0.06f, 0.16f, 0.2f, Color.white, new Color(1f, 0.85f, 0.45f));
            OneShot(flash, 22);
            Sphere(flash, 0.02f);
            F.Fade(flash, Color.white, new Color(1f, 0.5f, 0.2f), 0.4f);
            flash = F.SavePrefab(flash, "FX_Lar_Pop", folder);

            ParticleSystem smoke = F.NewSystem("FX_Lar_Smoke", alpha, 50, false);
            Main(smoke, 2f, 3.2f, 0.15f, 0.45f, 0.25f, 0.45f, -0.02f, new Color(0.62f, 0.58f, 0.55f, 0.25f), new Color(0.5f, 0.48f, 0.46f, 0.2f));
            Emit(smoke, 14f);
            var shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.2f, 0.02f, 0.1f);
            SmokeFade(smoke, 2.4f);
            smoke = F.SavePrefab(smoke, "FX_Lar_Smoke", folder);

            var model = new GameObject("Model_Lar");
            Material red = SceneKit.LitMaterial("Lar_Paper", new Color(0.78f, 0.12f, 0.12f), 0.3f);
            Material cord = SceneKit.LitMaterial("Lar_Cord", new Color(0.65f, 0.55f, 0.35f), 0.1f);
            SceneKit.Primitive(PrimitiveType.Cube, "Cord", model.transform, new Vector3(0f, 0.012f, 0f), new Vector3(pops * spacing + 0.02f, 0.006f, 0.006f), cord, false);
            for (int i = 0; i < pops; i += 2)
            {
                float x = 0.6f - i * spacing;
                Transform tube = SceneKit.Primitive(PrimitiveType.Cylinder, "Tube", model.transform, new Vector3(x, 0.014f, (i / 2 % 2 == 0 ? 0.012f : -0.012f)), new Vector3(0.012f, 0.018f, 0.012f), red, false).transform;
                tube.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            GameObject modelPrefab = F.SaveGameObject(model, "Model_Lar", folder);

            CrackerDefinition d = Load($"{Defs}/Cracker_ChainLar.asset");
            Common(d, "chain_lar", new LocalizedName("Chain crackers", "Laddi", "Ladi"), CrackerCategory.ChainCrackers, Intensity.Loud, 2, modelPrefab);
            d.Footprint = new Vector2(1.3f, 0.2f);
            d.EffectVolume = new Vector3(1.5f, 0.6f, 0.5f);
            d.FuseSeconds = 2f;
            d.FuseAnchor = new Vector3(0.62f, 0.014f, 0f);
            d.CooldownSeconds = 1.5f;
            Quality(d, 200, 300, 420);
            var s = d.MutableStages;
            s.Clear();
            s.Add(new StageDefinition { time = 0f, emitter = fuseSpark, anchor = StageAnchor.Fuse, endWithNextEmitter = true, sound = fuseHiss, loopSound = true, volume = 0.45f, haptic = HapticPattern.Tick });
            s.Add(new StageDefinition
            {
                time = 2f, emitter = flash, anchor = StageAnchor.Fuse, payoff = true, endLoopSound = true,
                repeatCount = pops, repeatInterval = interval, repeatOffset = new Vector3(-spacing, 0f, 0f), repeatPattern = RepeatPattern.Line,
                sound = pop, volume = 0.55f, haptic = HapticPattern.Tick, light = true, lightColor = new Color(1f, 0.85f, 0.6f), lightPeak = 1.2f,
            });
            s.Add(new StageDefinition { time = 2.05f, emitter = smoke });
            s.Add(new StageDefinition { time = 2f + pops * interval, hideModel = true });
            s.Add(new StageDefinition { time = 6.5f, stop = true });
            return Save(d);
        }

        // ---------------------------------------------------------------- Single bang

        private static CrackerDefinition Bomb(Material add, Material alpha, ParticleSystem fuseSpark, AudioClip fuseHiss)
        {
            const string folder = Root + "/Bomb";
            AudioClip boom = F.WriteClip("bomb_boom", HeavyBoom(F.SampleRate, 6301), false);

            ParticleSystem fire = F.NewSystem("FX_Bomb_Flash", add, 160, false);
            Main(fire, 0.12f, 0.32f, 4f, 9f, 0.18f, 0.45f, 0f, new Color(1f, 0.95f, 0.75f), new Color(1f, 0.55f, 0.15f));
            OneShot(fire, 150);
            Sphere(fire, 0.06f);
            F.Fade(fire, Color.white, new Color(1f, 0.35f, 0.05f), 0.6f);
            fire = F.SavePrefab(fire, "FX_Bomb_Flash", folder);

            ParticleSystem debris = F.NewSystem("FX_Bomb_Debris", alpha, 90, false);
            Main(debris, 0.9f, 1.8f, 2f, 4.5f, 0.02f, 0.05f, 1f, new Color(0.75f, 0.12f, 0.1f, 0.95f), new Color(0.5f, 0.32f, 0.2f, 0.95f));
            OneShot(debris, 80);
            Sphere(debris, 0.05f);
            debris = F.SavePrefab(debris, "FX_Bomb_Debris", folder);

            ParticleSystem smoke = F.NewSystem("FX_Bomb_Smoke", alpha, 30, false);
            Main(smoke, 2.4f, 4f, 0.4f, 1.2f, 0.5f, 1f, -0.04f, new Color(0.6f, 0.57f, 0.55f, 0.35f), new Color(0.48f, 0.46f, 0.45f, 0.3f));
            OneShot(smoke, 26);
            Sphere(smoke, 0.15f);
            SmokeFade(smoke, 2.8f);
            smoke = F.SavePrefab(smoke, "FX_Bomb_Smoke", folder);

            var model = new GameObject("Model_Bomb");
            Material twine = SceneKit.LitMaterial("Bomb_Twine", new Color(0.62f, 0.46f, 0.26f), 0.15f);
            Material band = SceneKit.LitMaterial("Bomb_Band", new Color(0.8f, 0.14f, 0.14f), 0.3f);
            SceneKit.Primitive(PrimitiveType.Sphere, "Ball", model.transform, new Vector3(0f, 0.065f, 0f), new Vector3(0.13f, 0.12f, 0.13f), twine, false);
            SceneKit.Primitive(PrimitiveType.Cylinder, "Band", model.transform, new Vector3(0f, 0.065f, 0f), new Vector3(0.135f, 0.012f, 0.135f), band, false);
            SceneKit.Primitive(PrimitiveType.Cylinder, "Fuse", model.transform, new Vector3(0f, 0.14f, 0f), new Vector3(0.006f, 0.02f, 0.006f), band, false);
            GameObject modelPrefab = F.SaveGameObject(model, "Model_Bomb", folder);

            CrackerDefinition d = Load($"{Defs}/Cracker_SingleBang.asset");
            Common(d, "bomb_single", new LocalizedName("Single bang", "Bam", "Bomb"), CrackerCategory.SingleBang, Intensity.Loud, 2, modelPrefab);
            d.Footprint = new Vector2(0.2f, 0.2f);
            d.EffectVolume = new Vector3(1.6f, 1.6f, 1.6f);
            d.FuseSeconds = 3f;
            d.FuseAnchor = new Vector3(0f, 0.16f, 0f);
            d.CooldownSeconds = 2f;
            Quality(d, 160, 160, 160);
            var s = d.MutableStages;
            s.Clear();
            s.Add(new StageDefinition { time = 0f, emitter = fuseSpark, anchor = StageAnchor.Fuse, endWithNextEmitter = true, sound = fuseHiss, loopSound = true, volume = 0.5f, haptic = HapticPattern.Tick });
            s.Add(new StageDefinition
            {
                time = 3f, emitter = fire, payoff = true, hideModel = true, endLoopSound = true, sound = boom, volume = 1f, duckOthers = 0.6f,
                light = true, lightColor = new Color(1f, 0.75f, 0.45f), lightPeak = 6f, haptic = HapticPattern.Thud, camera = CameraCue.Shake,
            });
            s.Add(new StageDefinition { time = 3f, emitter = debris });
            s.Add(new StageDefinition { time = 3.05f, emitter = smoke });
            s.Add(new StageDefinition { time = 6.5f, stop = true });
            return Save(d);
        }

        // ---------------------------------------------------------------- Skyshot

        private static CrackerDefinition Skyshot(Material add, ParticleSystem fuseSpark, AudioClip fuseHiss)
        {
            const string folder = Root + "/Skyshot";
            const float height = 9f;
            const float rise = 0.8f;
            AudioClip thump = F.WriteClip("skyshot_thump", ProceduralSounds.Generate(ProceduralSounds.Kind.Thump, F.SampleRate, 6401), false);
            AudioClip crackle = F.WriteClip("skyshot_crackle", ProceduralSounds.Generate(ProceduralSounds.Kind.Crackle, F.SampleRate, 6402), false);

            ParticleSystem trail = F.NewSystem("FX_Skyshot_Trail", add, 120, true);
            Main(trail, 0.2f, 0.45f, 0.1f, 0.6f, 0.025f, 0.05f, 0.4f, new Color(1f, 0.85f, 0.55f), new Color(1f, 0.55f, 0.2f));
            Emit(trail, 160f);
            Sphere(trail, 0.01f);
            F.Fade(trail, Color.white, new Color(1f, 0.4f, 0.1f), 0.2f);
            trail = F.SavePrefab(trail, "FX_Skyshot_Trail", folder);

            ParticleSystem burst = F.NewSystem("FX_Skyshot_Burst", add, 450, true);
            Main(burst, 1.1f, 1.6f, 4.5f, 6.5f, 0.05f, 0.09f, 0.35f, new Color(0.35f, 1f, 0.55f), new Color(0.35f, 0.85f, 1f));
            OneShot(burst, 420);
            Sphere(burst, 0.1f);
            Drag(burst, 1.3f);
            F.Fade(burst, Color.white, new Color(0.2f, 0.6f, 0.9f), 0.3f);
            burst = F.SavePrefab(burst, "FX_Skyshot_Burst", folder);

            ParticleSystem glitter = F.NewSystem("FX_Skyshot_Glitter", add, 160, false);
            Main(glitter, 0.1f, 0.3f, 0f, 0.3f, 0.07f, 0.14f, 0.2f, Color.white, new Color(0.8f, 1f, 0.85f));
            Emit(glitter, 260f);
            var gshape = glitter.shape;
            gshape.shapeType = ParticleSystemShapeType.Sphere;
            gshape.radius = 2f;
            var gmain = glitter.main;
            gmain.loop = false;
            gmain.duration = 0.7f;
            glitter = F.SavePrefab(glitter, "FX_Skyshot_Glitter", folder);

            var model = new GameObject("Model_Skyshot");
            Material card = SceneKit.LitMaterial("Skyshot_Card", new Color(0.18f, 0.35f, 0.75f), 0.3f);
            Material label = SceneKit.LitMaterial("Skyshot_Label", new Color(0.95f, 0.8f, 0.25f), 0.3f);
            Material dark = SceneKit.LitMaterial("Skyshot_Mouth", new Color(0.08f, 0.06f, 0.05f), 0.1f);
            SceneKit.Primitive(PrimitiveType.Cube, "Base", model.transform, new Vector3(0f, 0.015f, 0f), new Vector3(0.16f, 0.03f, 0.16f), card, false);
            SceneKit.Primitive(PrimitiveType.Cylinder, "Tube", model.transform, new Vector3(0f, 0.14f, 0f), new Vector3(0.07f, 0.11f, 0.07f), card, false);
            SceneKit.Primitive(PrimitiveType.Cylinder, "Label", model.transform, new Vector3(0f, 0.14f, 0f), new Vector3(0.072f, 0.04f, 0.072f), label, false);
            SceneKit.Primitive(PrimitiveType.Cylinder, "Mouth", model.transform, new Vector3(0f, 0.252f, 0f), new Vector3(0.05f, 0.003f, 0.05f), dark, false);
            GameObject modelPrefab = F.SaveGameObject(model, "Model_Skyshot", folder);

            CrackerDefinition d = Load($"{Defs}/Cracker_Skyshot.asset");
            Common(d, "skyshot_green", new LocalizedName("Skyshot", "Skyshot", "Skyshot"), CrackerCategory.Skyshot, Intensity.Loud, 2, modelPrefab);
            d.Footprint = new Vector2(0.2f, 0.2f);
            d.EffectVolume = new Vector3(1.5f, height + 1f, 1.5f);
            d.Surfaces = PlacementSurface.Ground | PlacementSurface.Rooftop | PlacementSurface.LaunchPad;
            d.FuseSeconds = 2f;
            d.FuseAnchor = new Vector3(0.05f, 0.04f, 0f);
            d.CooldownSeconds = 1.5f;
            Quality(d, 220, 380, 450);
            var s = d.MutableStages;
            s.Clear();
            s.Add(new StageDefinition { time = 0f, emitter = fuseSpark, anchor = StageAnchor.Fuse, endWithNextEmitter = true, sound = fuseHiss, loopSound = true, volume = 0.45f, haptic = HapticPattern.Tick });
            s.Add(new StageDefinition
            {
                time = 2f, emitter = trail, motion = StageMotion.Launch, launchHeight = height, launchRiseSeconds = rise, endLoopSound = true,
                sound = thump, volume = 0.85f, haptic = HapticPattern.Thud, camera = CameraCue.AerialFollow,
            });
            s.Add(new StageDefinition
            {
                time = 2f + rise, emitter = burst, anchor = StageAnchor.Apex, launchHeight = height, payoff = true, sound = crackle, volume = 0.8f,
                light = true, lightColor = new Color(0.4f, 1f, 0.7f), lightPeak = 3.5f, haptic = HapticPattern.Tick,
            });
            s.Add(new StageDefinition { time = 2.3f + rise, emitter = glitter, anchor = StageAnchor.Apex, launchHeight = height - 0.5f, sound = crackle, volume = 0.45f });
            s.Add(new StageDefinition { time = 6f, stop = true });
            return Save(d);
        }

        // ---------------------------------------------------------------- Multi-shot cake

        private static CrackerDefinition Cake(Material add, ParticleSystem fuseSpark, AudioClip fuseHiss)
        {
            const string folder = Root + "/Cake";
            const int shots = 12;
            const float interval = 0.55f;
            const float height = 8f;
            const float rise = 0.7f;
            var drift = new Vector3(0.25f, 0f, 0f);
            var spread = new Vector3(0.04f, 0f, 0f);
            AudioClip thump = F.WriteClip("cake_thump", ProceduralSounds.Generate(ProceduralSounds.Kind.Thump, F.SampleRate, 6501), false);
            AudioClip boom = F.WriteClip("cake_burst", ProceduralSounds.Generate(ProceduralSounds.Kind.Bang, F.SampleRate, 6502), false);

            ParticleSystem trail = F.NewSystem("FX_Cake_Trail", add, 80, true);
            Main(trail, 0.15f, 0.35f, 0.1f, 0.4f, 0.02f, 0.04f, 0.3f, new Color(1f, 0.9f, 0.6f), new Color(1f, 0.6f, 0.25f));
            Emit(trail, 140f);
            Sphere(trail, 0.008f);
            trail = F.SavePrefab(trail, "FX_Cake_Trail", folder);

            ParticleSystem burst = F.NewSystem("FX_Cake_Burst", add, 650, true);
            Main(burst, 1.2f, 1.8f, 5f, 7f, 0.06f, 0.1f, 0.3f, Color.white, Color.white);
            var main = burst.main;
            var colours = new Gradient();
            colours.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.3f, 0.75f), 0f), new GradientColorKey(new Color(1f, 0.78f, 0.2f), 0.33f),
                    new GradientColorKey(new Color(0.3f, 0.85f, 1f), 0.66f), new GradientColorKey(new Color(0.55f, 1f, 0.4f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(colours) { mode = ParticleSystemGradientMode.RandomColor };
            OneShot(burst, 520);
            Sphere(burst, 0.1f);
            Drag(burst, 1.4f);
            F.Fade(burst, Color.white, new Color(0.6f, 0.4f, 0.8f), 0.3f);
            burst = F.SavePrefab(burst, "FX_Cake_Burst", folder);

            var model = new GameObject("Model_Cake");
            Material box = SceneKit.LitMaterial("Cake_Box", new Color(0.72f, 0.12f, 0.16f), 0.35f);
            Material top = SceneKit.LitMaterial("Cake_Top", new Color(0.95f, 0.75f, 0.2f), 0.4f);
            Material hole = SceneKit.LitMaterial("Cake_Hole", new Color(0.06f, 0.04f, 0.04f), 0.1f);
            SceneKit.Primitive(PrimitiveType.Cube, "Box", model.transform, new Vector3(0f, 0.09f, 0f), new Vector3(0.3f, 0.18f, 0.24f), box, false);
            SceneKit.Primitive(PrimitiveType.Cube, "Top", model.transform, new Vector3(0f, 0.182f, 0f), new Vector3(0.302f, 0.006f, 0.242f), top, false);
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    SceneKit.Primitive(PrimitiveType.Cylinder, "Tube", model.transform, new Vector3(-0.105f + c * 0.07f, 0.186f, -0.07f + r * 0.07f), new Vector3(0.045f, 0.003f, 0.045f), hole, false);
                }
            }

            GameObject modelPrefab = F.SaveGameObject(model, "Model_Cake", folder);

            CrackerDefinition d = Load($"{Defs}/Cracker_Cake.asset");
            Common(d, "cake_twelve", new LocalizedName("Multi-shot cake", "Cake", "Cake"), CrackerCategory.MultiShotCake, Intensity.Loud, 3, modelPrefab);
            d.Footprint = new Vector2(0.32f, 0.26f);
            d.EffectVolume = new Vector3(3.4f, height + 1f, 1.5f);
            d.Surfaces = PlacementSurface.Ground | PlacementSurface.LaunchPad;
            d.FuseSeconds = 2.5f;
            d.FuseAnchor = new Vector3(0.16f, 0.06f, 0f);
            d.CooldownSeconds = 2f;
            Quality(d, 180, 350, 650);
            var s = d.MutableStages;
            s.Clear();
            s.Add(new StageDefinition { time = 0f, emitter = fuseSpark, anchor = StageAnchor.Fuse, endWithNextEmitter = true, sound = fuseHiss, loopSound = true, volume = 0.45f, haptic = HapticPattern.Tick });
            s.Add(new StageDefinition
            {
                time = 2.5f, emitter = trail, motion = StageMotion.Launch, launchHeight = height, launchRiseSeconds = rise, launchDrift = drift,
                repeatCount = shots, repeatInterval = interval, repeatOffset = spread, repeatPattern = RepeatPattern.Fan,
                endLoopSound = true, sound = thump, volume = 0.75f, haptic = HapticPattern.Tick, camera = CameraCue.AerialFollow,
            });
            s.Add(new StageDefinition
            {
                time = 2.5f + rise, emitter = burst, anchor = StageAnchor.Apex, launchHeight = height, launchDrift = drift, payoff = true,
                repeatCount = shots, repeatInterval = interval, repeatOffset = spread, repeatPattern = RepeatPattern.Fan,
                sound = boom, volume = 0.8f, duckOthers = 0.3f, light = true, lightColor = new Color(1f, 0.7f, 0.9f), lightPeak = 3f, haptic = HapticPattern.Thud,
            });
            s.Add(new StageDefinition { time = 2.5f + rise + (shots - 1) * interval + 2.5f, stop = true });
            return Save(d);
        }

        // ---------------------------------------------------------------- helpers

        private static CrackerDefinition Load(string path)
        {
            var d = AssetDatabase.LoadAssetAtPath<CrackerDefinition>(path);
            if (d == null)
            {
                d = ScriptableObject.CreateInstance<CrackerDefinition>();
                AssetDatabase.CreateAsset(d, path);
            }

            return d;
        }

        private static CrackerDefinition Save(CrackerDefinition d)
        {
            EditorUtility.SetDirty(d);
            return d;
        }

        private static void Common(CrackerDefinition d, string id, LocalizedName name, CrackerCategory category, Intensity intensity, int cost, GameObject model)
        {
            d.Id = id;
            d.Schema = CrackerDefinition.CurrentSchema;
            d.DisplayName = name;
            d.Category = category;
            d.SetFestivals("diwali", "tihar");
            d.Intensity = intensity;
            d.Surfaces = PlacementSurface.Ground;
            d.IgnitionMethods = IgnitionMethod.Lighter | IgnitionMethod.Match;
            d.ModelPrefab = model;
            d.Voices = 3;
            d.Cost = cost;
            d.Pack = "core";
            d.CreditsModel = "own";
            d.CreditsAudio = "own-003";
        }

        private static void Quality(CrackerDefinition d, int low, int mid, int high)
        {
            d.SetQuality(QualityTier.Low, new TierQuality(low, false, false));
            d.SetQuality(QualityTier.Mid, new TierQuality(mid, true, false));
            d.SetQuality(QualityTier.High, new TierQuality(high, true, true));
        }

        private static void Main(ParticleSystem ps, float lifeMin, float lifeMax, float speedMin, float speedMax, float sizeMin, float sizeMax, float gravity, Color a, Color b)
        {
            ParticleSystem.MainModule m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            m.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            m.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            m.gravityModifier = gravity;
            m.startColor = new ParticleSystem.MinMaxGradient(a, b);
        }

        private static void Emit(ParticleSystem ps, float rate)
        {
            ParticleSystem.EmissionModule e = ps.emission;
            e.rateOverTime = rate;
        }

        private static void OneShot(ParticleSystem ps, int count)
        {
            ParticleSystem.MainModule m = ps.main;
            m.loop = false;
            m.duration = 0.2f;
            ParticleSystem.EmissionModule e = ps.emission;
            e.rateOverTime = 0f;
            e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        private static void Sphere(ParticleSystem ps, float radius)
        {
            ParticleSystem.ShapeModule s = ps.shape;
            s.shapeType = ParticleSystemShapeType.Sphere;
            s.radius = radius;
        }

        private static void Drag(ParticleSystem ps, float drag)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 100f;
            limit.drag = drag;
        }

        private static void SmokeFade(ParticleSystem ps, float grow)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, grow));
            ParticleSystem.MainModule m = ps.main;
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        }

        /// <summary>One-second loopable sizzle: bright hiss with dense micro-crackles.</summary>
        private static float[] Sizzle(int rate, int seed)
        {
            var rng = new Random(seed);
            int n = rate;
            var d = new float[n];
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float hp = noise - 0.9f * prev;
                prev = noise;
                float crackle = rng.NextDouble() < 0.004 ? (float)(rng.NextDouble() * 2.0 - 1.0) * 3f : 0f;
                d[i] = hp * 0.4f + crackle;
            }

            for (int i = 1; i < n; i++)
            {
                d[i] = d[i] * 0.7f + d[i - 1] * 0.3f;
            }

            return LoopAndNormalize(d, rate / 50, 0.8f);
        }

        /// <summary>A deeper, longer boom than the rocket burst: sub thump, crack and rumbling tail.</summary>
        private static float[] HeavyBoom(int rate, int seed)
        {
            var rng = new Random(seed);
            int n = (int)(rate * 1.8f);
            var d = new float[n];
            double phase = 0.0;
            float low = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                double freq = 38.0 + 70.0 * Math.Exp(-t * 9.0);
                phase += 2.0 * Math.PI * freq / rate;
                float sub = (float)Math.Sin(phase) * (float)Math.Exp(-t * 3.2);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += 0.04f * (noise - low);
                float rumble = low * 4f * (float)Math.Exp(-t * 2.2);
                float crack = noise * (float)Math.Exp(-t * 55.0);
                d[i] = (float)Math.Tanh(sub * 1.3f + rumble + crack * 0.9f);
            }

            return LoopAndNormalize(d, 0, 0.95f);
        }

        private static float[] LoopAndNormalize(float[] d, int fade, float peak)
        {
            int n = d.Length;
            for (int i = 0; i < fade; i++)
            {
                float g = (float)i / fade;
                d[i] *= g;
                d[n - 1 - i] *= g;
            }

            float max = 0f;
            foreach (float s in d)
            {
                max = Math.Max(max, Math.Abs(s));
            }

            for (int i = 0; i < n && max > 0f; i++)
            {
                d[i] = d[i] / max * peak;
            }

            return d;
        }
    }
}

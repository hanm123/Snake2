using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Media;
using System.Windows.Forms;

namespace SnakeGameWin;

enum GameState { Menu, Playing, Paused, Dead, Won }
enum WallMode { Walls, Wrap }
enum Difficulty { Easy, Normal, Hard }
enum ObstacleLevel { Off, Few, Many }

record Skin(string Name, Color Head, Color[] Body, Color Tongue);

class SnakeForm : Form
{
    const int Cols = 40;
    const int Rows = 20;
    const int Cell = 24;
    const float Margin = 16f;
    const float HudH = 56f;
    const float BoardTop = 72f;
    const int WidthL = (int)(2 * Margin + Cols * Cell);
    const int HeightL = (int)(BoardTop + Rows * Cell + 24);

    static readonly Color Bg = Color.FromArgb(13, 16, 20);
    static readonly Color CellA = Color.FromArgb(18, 21, 27);
    static readonly Color CellB = Color.FromArgb(20, 23, 29);
    static readonly Color Neon = Color.FromArgb(80, 230, 255);
    static readonly Color FoodCol = Color.FromArgb(255, 214, 80);
    static readonly Color BonusCol = Color.FromArgb(200, 120, 255);
    static readonly Color BoostCol = Color.FromArgb(255, 170, 40);

    static readonly Skin[] Skins =
    {
        new("霓虹", Color.FromArgb(255, 216, 70), new[]
        {
            Color.FromArgb(0, 229, 255), Color.FromArgb(60, 140, 255),
            Color.FromArgb(120, 90, 255), Color.FromArgb(255, 90, 200),
            Color.FromArgb(0, 200, 130)
        }, Color.FromArgb(255, 80, 100)),
        new("烈焰", Color.FromArgb(255, 240, 180), new[]
        {
            Color.FromArgb(255, 200, 60), Color.FromArgb(255, 140, 30),
            Color.FromArgb(255, 70, 30), Color.FromArgb(220, 30, 60),
            Color.FromArgb(160, 20, 80)
        }, Color.FromArgb(255, 120, 50)),
        new("翡翠", Color.FromArgb(210, 255, 210), new[]
        {
            Color.FromArgb(80, 255, 160), Color.FromArgb(40, 220, 120),
            Color.FromArgb(20, 180, 90), Color.FromArgb(30, 140, 100),
            Color.FromArgb(20, 100, 80)
        }, Color.FromArgb(255, 100, 120)),
        new("紫罗兰", Color.FromArgb(240, 220, 255), new[]
        {
            Color.FromArgb(200, 140, 255), Color.FromArgb(170, 90, 240),
            Color.FromArgb(140, 50, 220), Color.FromArgb(220, 80, 180),
            Color.FromArgb(255, 120, 200)
        }, Color.FromArgb(255, 150, 220)),
        new("黄金", Color.FromArgb(255, 250, 200), new[]
        {
            Color.FromArgb(255, 220, 100), Color.FromArgb(240, 180, 50),
            Color.FromArgb(200, 140, 30), Color.FromArgb(160, 100, 20),
            Color.FromArgb(120, 70, 10)
        }, Color.FromArgb(255, 180, 80)),
        new("极光", Color.FromArgb(220, 255, 255), new[]
        {
            Color.FromArgb(100, 255, 210), Color.FromArgb(80, 200, 255),
            Color.FromArgb(120, 140, 255), Color.FromArgb(200, 100, 255),
            Color.FromArgb(255, 120, 180)
        }, Color.FromArgb(150, 255, 220)),
        new("彩虹", Color.FromArgb(255, 255, 255), new[]
        {
            Color.FromArgb(255, 60, 60), Color.FromArgb(255, 160, 40),
            Color.FromArgb(255, 230, 60), Color.FromArgb(80, 220, 100),
            Color.FromArgb(60, 160, 255), Color.FromArgb(160, 80, 220),
            Color.FromArgb(255, 100, 200)
        }, Color.FromArgb(255, 100, 100)),
        new("像素", Color.FromArgb(180, 255, 100), new[]
        {
            Color.FromArgb(120, 255, 80), Color.FromArgb(80, 220, 60),
            Color.FromArgb(50, 180, 40), Color.FromArgb(30, 140, 30),
            Color.FromArgb(20, 100, 20)
        }, Color.FromArgb(255, 80, 80)),
        new("金属", Color.FromArgb(240, 240, 250), new[]
        {
            Color.FromArgb(200, 210, 220), Color.FromArgb(160, 170, 185),
            Color.FromArgb(120, 130, 150), Color.FromArgb(150, 160, 175),
            Color.FromArgb(190, 200, 215)
        }, Color.FromArgb(255, 80, 80)),
    };

    readonly List<(int X, int Y)> cells = new();
    readonly List<PointF> body = new();
    readonly List<(int X, int Y)> obstacles = new();
    readonly List<(int X, int Y, float Timer)> warnings = new();
    readonly Queue<(int dx, int dy)> dirQueue = new();
    readonly List<Particle> parts = new();
    readonly List<AmbientParticle> ambient = new();
    readonly List<DecorSnake> decorSnakes = new();
    readonly System.Windows.Forms.Timer timer;
    readonly Stopwatch clock = new();
    readonly Dictionary<int, SolidBrush> brushCache = new();

    Font fBig = null!, fMid = null!, fHint = null!, fTitle = null!, fMenu = null!, fMenuSel = null!;

    GameState state = GameState.Menu;
    WallMode wallMode = WallMode.Walls;
    Difficulty difficulty = Difficulty.Normal;
    ObstacleLevel obstacleLevel = ObstacleLevel.Few;
    int skinIndex;
    int menuIndex;

    Skin CurrentSkin => Skins[skinIndex];
    Color CurrentHead => CurrentSkin.Head;
    Color[] CurrentPal => CurrentSkin.Body;
    Color CurrentTongue => CurrentSkin.Tongue;

    (int X, int Y) food = (-1, -1);
    (int X, int Y) bonus = (-1, -1);
    float bonusTimer;
    (int X, int Y) speedBoost = (-1, -1);
    float speedBoostTimer;
    float boostActive;
    (int X, int Y) star = (-1, -1);
    float starTimer;
    float invincible;
    (int X, int Y) slow = (-1, -1);
    float slowTimer;
    float slowActive;
    (int X, int Y) doubleP = (-1, -1);
    float doubleTimer;
    float doubleActive;
    float obstacleSpawnTimer;
    (float vx, float vy) visualDir = (1, 0);
    (int dx, int dy) dir = (1, 0);
    int baseInterval;
    int EffectiveInterval
    {
        get
        {
            int iv = baseInterval;
            // 等级加速：每级 -5%，最低 40ms
            iv = (int)(iv * MathF.Pow(0.95f, level - 1));
            if (iv < 40) iv = 40;
            if (boostActive > 0) iv = (int)(iv * 0.55f);
            if (slowActive > 0) iv = (int)(iv * 1.6f);
            return iv;
        }
    }
    int ScoreMult => doubleActive > 0 ? 2 : 1;
    int score;
    int highScore;
    int combo = 1;
    float comboTimer;
    float gameT;
    float acc;
    long lastMs;

    float shakeX, shakeY, shakeMag, shakeTime;
    float tongueT, tongueCd;
    bool muted;
    bool fullscreen;

    // 等级系统
    int level = 1;
    string levelUpText = "";
    float levelUpTimer;

    // 死亡动画
    float deathAnim = -1; // <0 = 无, 0~1 = 播放中

    // 飘字
    readonly List<FloatingText> floats = new();

    // 暂停菜单
    int pauseIndex;

    // 食物弹出动画
    float foodSpawnAnim = 1, bonusSpawnAnim = 1, boostSpawnAnim = 1, starSpawnAnim = 1, slowSpawnAnim = 1, doubleSpawnAnim = 1;

    // 转场
    float transition; // 0=透明 1=全黑
    int transitionDir; // -1淡出 1淡入 0无

    // 特效
    float flashAlpha;
    Color flashColor = Color.White;
    readonly List<Shockwave> shockwaves = new();
    readonly List<PointF> trail = new(); // 蛇头轨迹残影

    // 背景音乐
    bool bgmOn = true;

    SoundPlayer? sndEat, sndBonus, sndDie, sndTurn, sndStart, sndMenu, sndBoost, sndInvincible, sndBreak, sndWarn, sndSlow, sndDouble, sndLevelUp;

    SnakeForm()
    {
        float k = DeviceDpi / 96f;
        fBig = new Font("Microsoft YaHei UI", 8f * k, FontStyle.Bold);
        fMid = new Font("Microsoft YaHei UI", 5.5f * k, FontStyle.Bold);
        fHint = new Font("Microsoft YaHei UI", 4.5f * k);
        fTitle = new Font("Microsoft YaHei UI", 22f * k, FontStyle.Bold);
        fMenu = new Font("Microsoft YaHei UI", 8f * k);
        fMenuSel = new Font("Microsoft YaHei UI", 8.5f * k, FontStyle.Bold);

        Text = "贪吃蛇 Snake";
        ClientSize = new Size((int)(WidthL * k), (int)(HeightL * k));
        MinimumSize = new Size((int)(WidthL * k * 0.5f), (int)(HeightL * k * 0.5f));
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg;
        DoubleBuffered = true;
        KeyPreview = true;
        KeyDown += OnKey;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        ResizeRedraw = true;

        LoadHighScore();
        LoadSettings();
        InitAmbient();
        InitDecorSnakes();
        InitSounds();

        timer = new System.Windows.Forms.Timer { Interval = 15 };
        timer.Tick += (_, _) => OnTick();
        clock.Start();
        timer.Start();
    }

    // ── 难度参数 ──────────────────────────────────────────────
    (int startInterval, int speedStep, int minInterval) DifficultySettings => difficulty switch
    {
        Difficulty.Easy => (180, 3, 90),
        Difficulty.Normal => (150, 4, 60),
        Difficulty.Hard => (110, 5, 40),
        _ => (150, 4, 60)
    };

    // ── 最高分持久化 ──────────────────────────────────────────
    static string HighScorePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SnakeGameWin", "highscore.txt");

    void LoadHighScore()
    {
        try
        {
            if (File.Exists(HighScorePath))
                highScore = int.Parse(File.ReadAllText(HighScorePath).Trim());
        }
        catch { highScore = 0; }
    }

    void SaveHighScore()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HighScorePath)!);
            File.WriteAllText(HighScorePath, highScore.ToString());
        }
        catch { }
    }

    // ── 设置持久化 ────────────────────────────────────────────
    static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SnakeGameWin", "settings.txt");

    void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var lines = File.ReadAllLines(SettingsPath);
            foreach (var line in lines)
            {
                var kv = line.Split('=');
                if (kv.Length != 2) continue;
                switch (kv[0].Trim())
                {
                    case "skin": skinIndex = Math.Clamp(int.Parse(kv[1]), 0, Skins.Length - 1); break;
                    case "difficulty": difficulty = Enum.Parse<Difficulty>(kv[1].Trim()); break;
                    case "wallMode": wallMode = Enum.Parse<WallMode>(kv[1].Trim()); break;
                    case "obstacle": obstacleLevel = Enum.Parse<ObstacleLevel>(kv[1].Trim()); break;
                    case "muted": muted = bool.Parse(kv[1].Trim()); break;
                    case "bgm": bgmOn = bool.Parse(kv[1].Trim()); break;
                }
            }
        }
        catch { }
    }

    void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllLines(SettingsPath, new[]
            {
                $"skin={skinIndex}",
                $"difficulty={difficulty}",
                $"wallMode={wallMode}",
                $"obstacle={obstacleLevel}",
                $"muted={muted}",
                $"bgm={bgmOn}",
            });
        }
        catch { }
    }

    void CheckHighScore()
    {
        if (score > highScore)
        {
            highScore = score;
            SaveHighScore();
        }
    }

    // ── 音效 ──────────────────────────────────────────────────
    void InitSounds()
    {
        sndEat = MakeSound(new[] { (880, 25), (1175, 20) }, 0.18f);
        sndBonus = MakeSound(new[] { (660, 50), (880, 50), (1175, 70), (1568, 90) }, 0.2f);
        sndBoost = MakeSound(new[] { (440, 30), (660, 30), (880, 30), (1320, 80) }, 0.2f);
        sndInvincible = MakeSound(new[] { (523, 40), (659, 40), (784, 40), (1047, 60), (1319, 100) }, 0.2f);
        sndBreak = MakeSound(new[] { (180, 30), (120, 40), (80, 60) }, 0.22f);
        sndWarn = MakeSound(new[] { (440, 40), (440, 40) }, 0.1f);
        sndSlow = MakeSound(new[] { (660, 50), (440, 60), (330, 80) }, 0.18f);
        sndDouble = MakeSound(new[] { (523, 30), (659, 30), (784, 30), (1047, 60) }, 0.18f);
        sndDie = MakeSound(new[] { (320, 90), (220, 110), (140, 180), (90, 220) }, 0.26f);
        sndTurn = MakeSound(new[] { (520, 22) }, 0.07f);
        sndStart = MakeSound(new[] { (440, 60), (660, 70), (880, 90) }, 0.18f);
        sndMenu = MakeSound(new[] { (720, 28) }, 0.09f);
        sndLevelUp = MakeSound(new[] { (523, 50), (659, 50), (784, 50), (1047, 80), (1319, 120) }, 0.2f);
        InitBgm();
    }

    short[]? bgmSamples;
    SoundPlayer? bgmPlayer;
    string? bgmPath;
    volatile bool bgmShouldPlay;

    void InitBgm()
    {
        const int sr = 44100;
        var samples = new List<short>(sr * 10);
        int[] melody = { 523, 659, 784, 659, 523, 659, 784, 1047, 784, 659, 523, 587, 659, 523, 440, 523 };
        int[] bass = { 131, 131, 196, 196, 175, 175, 147, 147 };
        int noteMs = 280;
        int bassMs = 560;
        for (int bar = 0; bar < 2; bar++)
        {
            for (int i = 0; i < melody.Length; i++)
            {
                int n = sr * noteMs / 1000;
                for (int j = 0; j < n; j++)
                {
                    float t = (float)j / sr;
                    float env = MathF.Min(1f, j / 200f) * MathF.Min(1f, (n - j) / 300f);
                    float sq = MathF.Sign(MathF.Sin(2 * MathF.PI * melody[i] * t));
                    short v = (short)(sq * env * 6000);
                    samples.Add(v);
                }
            }
            for (int i = 0; i < bass.Length; i++)
            {
                int n = sr * bassMs / 1000;
                for (int j = 0; j < n; j++)
                {
                    float t = (float)j / sr;
                    float env = MathF.Min(1f, j / 300f) * MathF.Min(1f, (n - j) / 400f);
                    float sq = MathF.Sign(MathF.Sin(2 * MathF.PI * bass[i] * t));
                    int idx = (bar * bass.Length + i) * (sr * bassMs / 1000) + j;
                    if (idx < samples.Count)
                        samples[idx] = (short)Math.Clamp(samples[idx] + sq * env * 4000, short.MinValue, short.MaxValue);
                }
            }
        }
        bgmSamples = samples.ToArray();
        try
        {
            bgmPath = Path.Combine(Path.GetTempPath(), "snake_bgm.wav");
            File.WriteAllBytes(bgmPath, WriteWav(samples));
            bgmPlayer = new SoundPlayer(bgmPath);
        }
        catch { bgmPath = null; }
    }

    void StartBgm()
    {
        if (muted || !bgmOn || bgmPlayer == null) return;
        bgmShouldPlay = true;
        try { bgmPlayer.PlayLooping(); } catch { }
    }

    void StopBgm()
    {
        bgmShouldPlay = false;
        try { bgmPlayer?.Stop(); } catch { }
    }

    void Play(SoundPlayer? s)
    {
        if (muted || s == null) return;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try { s.PlaySync(); } catch { }
            // 音效播完立即恢复 BGM
            if (bgmShouldPlay && !muted && bgmOn && bgmPlayer != null)
            {
                try { bgmPlayer.PlayLooping(); } catch { }
            }
        });
    }

    static SoundPlayer MakeSound((int freq, int ms)[] notes, float vol)
    {
        const int sr = 44100;
        var samples = new List<short>(sr);
        foreach (var (freq, ms) in notes)
        {
            int n = sr * ms / 1000;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float total = MathF.Max(0.05f, ms / 1000f);
                float env = MathF.Exp(-t * (10f / total));
                float s = MathF.Sin(2 * MathF.PI * freq * t) * 0.72f +
                          MathF.Sin(2 * MathF.PI * freq * 2f * t) * 0.16f +
                          MathF.Sin(2 * MathF.PI * freq * 0.5f * t) * 0.12f;
                samples.Add((short)(s * vol * 32767f * env));
            }
        }
        return new SoundPlayer(new MemoryStream(WriteWav(samples)));
    }

    static byte[] WriteWav(List<short> samples)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        int dataLen = samples.Count * 2;
        bw.Write(new[] { 'R', 'I', 'F', 'F' });
        bw.Write(36 + dataLen);
        bw.Write(new[] { 'W', 'A', 'V', 'E' });
        bw.Write(new[] { 'f', 'm', 't', ' ' });
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)1);
        bw.Write(44100);
        bw.Write(44100 * 2);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write(new[] { 'd', 'a', 't', 'a' });
        bw.Write(dataLen);
        foreach (var s in samples) bw.Write(s);
        return ms.ToArray();
    }

    // ── 环境背景粒子 ──────────────────────────────────────────
    void InitAmbient()
    {
        for (int i = 0; i < 35; i++)
        {
            ambient.Add(new AmbientParticle
            {
                X = Random.Shared.Next(WidthL),
                Y = Random.Shared.Next(HeightL),
                Vx = (float)(Random.Shared.NextDouble() * 6 - 3),
                Vy = (float)(Random.Shared.NextDouble() * 4 - 2),
                R = 0.6f + (float)Random.Shared.NextDouble() * 1.4f,
                Alpha = 18 + Random.Shared.Next(38)
            });
        }
    }

    void InitDecorSnakes()
    {
        decorSnakes.Clear();
        var rng = Random.Shared;
        for (int i = 0; i < 5; i++)
        {
            var ds = new DecorSnake
            {
                length = 20 + rng.Next(12),
                segGap = 13f,
                speed = 18 + rng.Next(28),
                alpha = 0.10f + (float)rng.NextDouble() * 0.10f,
                pal = Skins[rng.Next(Skins.Length)].Body
            };
            ds.dx = rng.NextDouble() > 0.5 ? 1f : -1f;
            ds.dy = (float)(rng.NextDouble() * 0.25 - 0.125);
            float dl = MathF.Sqrt(ds.dx * ds.dx + ds.dy * ds.dy);
            ds.dx /= dl; ds.dy /= dl;
            float sx = rng.Next(WidthL);
            float sy = 70 + rng.Next(HeightL - 110);
            for (int j = 0; j < ds.length; j++)
                ds.segs.Add(new PointF(sx - ds.dx * ds.segGap * j, sy - ds.dy * ds.segGap * j));
            decorSnakes.Add(ds);
        }
    }

    // ── 障碍物参数 ────────────────────────────────────────────
    (float interval, int max) ObstacleSettings => obstacleLevel switch
    {
        ObstacleLevel.Off => (0, 0),
        ObstacleLevel.Few => (11000, 8),
        ObstacleLevel.Many => (7000, 16),
        _ => (0, 0)
    };

    int MaxObstacles => ObstacleSettings.max + difficulty switch { Difficulty.Easy => 0, Difficulty.Normal => 2, Difficulty.Hard => 4, _ => 0 };
    float SpawnInterval => MathF.Max(3000, ObstacleSettings.interval - difficulty switch { Difficulty.Easy => 0, Difficulty.Normal => 1000, Difficulty.Hard => 2000, _ => 0 });

    void SpawnWarning()
    {
        (int X, int Y) head = cells.Count > 0 ? cells[0] : (Cols / 2, Rows / 2);
        for (int tries = 0; tries < 200; tries++)
        {
            var c = (X: Random.Shared.Next(Cols), Y: Random.Shared.Next(Rows));
            if (cells.Contains(c)) continue;
            if (obstacles.Contains(c)) continue;
            if (warnings.Any(w => w.X == c.X && w.Y == c.Y)) continue;
            if (c == food || c == bonus || c == speedBoost || c == star || c == slow || c == doubleP) continue;
            if (Math.Abs(head.X - c.X) <= 3 && Math.Abs(head.Y - c.Y) <= 2) continue;
            warnings.Add((c.X, c.Y, 1800));
            Play(sndWarn);
            return;
        }
    }

    // ── 游戏逻辑 ──────────────────────────────────────────────
    void ResetGame()
    {
        cells.Clear();
        body.Clear();
        parts.Clear();
        dirQueue.Clear();
        obstacles.Clear();
        warnings.Clear();
        cells.Add((Cols / 2, Rows / 2));
        cells.Add((Cols / 2 - 1, Rows / 2));
        cells.Add((Cols / 2 - 2, Rows / 2));
        body.AddRange(cells.Select(Center));
        dir = (1, 0);
        var (si, _, _) = DifficultySettings;
        baseInterval = si;
        boostActive = 0;
        invincible = 0;
        obstacleSpawnTimer = 4000;
        score = 0;
        combo = 1;
        comboTimer = 0;
        bonus = (-1, -1);
        bonusTimer = 0;
        speedBoost = (-1, -1);
        speedBoostTimer = 0;
        star = (-1, -1);
        starTimer = 0;
        slow = (-1, -1);
        slowTimer = 0;
        slowActive = 0;
        doubleP = (-1, -1);
        doubleTimer = 0;
        doubleActive = 0;
        visualDir = (1, 0);
        gameT = 0;
        acc = 0;
        shakeTime = 0;
        tongueCd = 1500;
        tongueT = 0;
        level = 1;
        levelUpText = "";
        levelUpTimer = 0;
        deathAnim = -1;
        floats.Clear();
        trail.Clear();
        shockwaves.Clear();
        flashAlpha = 0;
        pauseIndex = 0;
        foodSpawnAnim = 0;
        bonusSpawnAnim = 1;
        boostSpawnAnim = 1;
        starSpawnAnim = 1;
        slowSpawnAnim = 1;
        doubleSpawnAnim = 1;
        food = NewFood();
    }

    void StartGame()
    {
        ResetGame();
        state = GameState.Playing;
        transition = 1f;
        transitionDir = -1; // 淡出（从全黑到透明）
        Play(sndStart);
        StartBgm();
    }

    static PointF Center((int X, int Y) c) =>
        new(Margin + (c.X + .5f) * Cell, BoardTop + (c.Y + .5f) * Cell);

    // 弹出动画：0→1 带过冲
    static float SpawnScale(float anim)
    {
        if (anim >= 1f) return 1f;
        float t = anim;
        return 1f + 2.7f * MathF.Pow(t - 1f, 3f) + 1.7f * MathF.Pow(t - 1f, 2f);
    }

    // ── 输入 ──────────────────────────────────────────────────
    void OnKey(object? sender, KeyEventArgs e)
    {
        if (state == GameState.Menu)
        {
            OnMenuKey(e);
            return;
        }
        if (state == GameState.Paused)
        {
            switch (e.KeyCode)
            {
                case Keys.Up or Keys.W:
                    pauseIndex = (pauseIndex + 2) % 3;
                    Play(sndMenu);
                    break;
                case Keys.Down or Keys.S:
                    pauseIndex = (pauseIndex + 1) % 3;
                    Play(sndMenu);
                    break;
                case Keys.Enter:
                    if (pauseIndex == 0) state = GameState.Playing;
                    else if (pauseIndex == 1) StartGame();
                    else { state = GameState.Menu; StopBgm(); }
                    break;
                case Keys.Space or Keys.P or Keys.Escape:
                    state = GameState.Playing;
                    break;
            }
            return;
        }
        switch (e.KeyCode)
        {
            case Keys.Up or Keys.W: TryDir(0, -1); break;
            case Keys.Down or Keys.S: TryDir(0, 1); break;
            case Keys.Left or Keys.A: TryDir(-1, 0); break;
            case Keys.Right or Keys.D: TryDir(1, 0); break;
            case Keys.Space or Keys.P:
                if (state == GameState.Playing) state = GameState.Paused;
                else if (state == GameState.Paused) state = GameState.Playing;
                break;
            case Keys.R:
                if (state is GameState.Dead or GameState.Won or GameState.Paused)
                    StartGame();
                break;
            case Keys.M:
                muted = !muted;
                if (muted) StopBgm(); else if (state == GameState.Playing) StartBgm();
                SaveSettings();
                break;
            case Keys.B:
                bgmOn = !bgmOn;
                if (bgmOn && state == GameState.Playing && !muted) StartBgm();
                else StopBgm();
                SaveSettings();
                break;
            case Keys.F11: ToggleFullscreen(); break;
            case Keys.Escape:
                state = GameState.Menu;
                StopBgm();
                break;
        }
    }

    void OnMenuKey(KeyEventArgs e)
    {
        const int count = 6;
        switch (e.KeyCode)
        {
            case Keys.Up or Keys.W:
                menuIndex = (menuIndex + count - 1) % count;
                Play(sndMenu);
                break;
            case Keys.Down or Keys.S:
                menuIndex = (menuIndex + 1) % count;
                Play(sndMenu);
                break;
            case Keys.Left or Keys.A:
                if (menuIndex == 0)
                    wallMode = wallMode == WallMode.Walls ? WallMode.Wrap : WallMode.Walls;
                else if (menuIndex == 1)
                    difficulty = (Difficulty)(((int)difficulty + 2) % 3);
                else if (menuIndex == 2)
                    obstacleLevel = (ObstacleLevel)(((int)obstacleLevel + 2) % 3);
                else if (menuIndex == 3)
                    skinIndex = (skinIndex + Skins.Length - 1) % Skins.Length;
                SaveSettings();
                Play(sndMenu);
                break;
            case Keys.Right or Keys.D:
                if (menuIndex == 0)
                    wallMode = wallMode == WallMode.Walls ? WallMode.Wrap : WallMode.Walls;
                else if (menuIndex == 1)
                    difficulty = (Difficulty)(((int)difficulty + 1) % 3);
                else if (menuIndex == 2)
                    obstacleLevel = (ObstacleLevel)(((int)obstacleLevel + 1) % 3);
                else if (menuIndex == 3)
                    skinIndex = (skinIndex + 1) % Skins.Length;
                SaveSettings();
                Play(sndMenu);
                break;
            case Keys.Enter or Keys.Space:
                if (menuIndex == 4) StartGame();
                else if (menuIndex == 5) Close();
                break;
            case Keys.Escape: Close(); break;
        }
    }

    void ToggleFullscreen()
    {
        fullscreen = !fullscreen;
        if (fullscreen)
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Normal;
        }
    }

    void TryDir(int dx, int dy)
    {
        if (state != GameState.Playing) return;
        var last = dirQueue.Count > 0 ? dirQueue.Last() : dir;
        if (dx == -last.dx && dy == -last.dy) return;
        if (dx == last.dx && dy == last.dy) return;
        if (dirQueue.Count < 2)
        {
            dirQueue.Enqueue((dx, dy));
            // 转向音效已移除：避免频繁打断 BGM
        }
    }

    // ── 主循环 ────────────────────────────────────────────────
    void OnTick()
    {
        long now = clock.ElapsedMilliseconds;
        int dt = (int)Math.Clamp(now - lastMs, 0, 100);
        lastMs = now;

        gameT += dt;

        foreach (var a in ambient) a.Step(dt, WidthL, HeightL);
        foreach (var ds in decorSnakes) ds.Step(dt, WidthL, HeightL);

        // 屏幕震动衰减
        if (shakeTime > 0)
        {
            shakeTime -= dt;
            float mag = shakeMag * MathF.Max(0, shakeTime / 500f);
            shakeX = (float)(Random.Shared.NextDouble() * 2 - 1) * mag;
            shakeY = (float)(Random.Shared.NextDouble() * 2 - 1) * mag;
        }
        else { shakeX = 0; shakeY = 0; }

        // 蛇信子
        tongueCd -= dt;
        if (tongueCd <= 0 && tongueT <= 0)
        {
            tongueT = 1f;
            tongueCd = 1500 + Random.Shared.Next(1800);
        }
        if (tongueT > 0) tongueT = MathF.Max(0, tongueT - dt / 260f);

        if (state == GameState.Playing)
        {
            acc += dt;
            comboTimer -= dt;
            if (comboTimer <= 0) combo = 1;

            if (boostActive > 0) boostActive -= dt;
            if (invincible > 0) invincible -= dt;
            if (slowActive > 0) slowActive -= dt;
            if (doubleActive > 0) doubleActive -= dt;

            // 平滑朝向插值
            {
                float f = 1f - MathF.Exp(-dt / 70f);
                visualDir = (
                    visualDir.vx + (dir.dx - visualDir.vx) * f,
                    visualDir.vy + (dir.dy - visualDir.vy) * f);
            }

            if (bonus.X >= 0)
            {
                bonusTimer -= dt;
                if (bonusTimer <= 0) bonus = (-1, -1);
            }
            if (speedBoost.X >= 0)
            {
                speedBoostTimer -= dt;
                if (speedBoostTimer <= 0) speedBoost = (-1, -1);
            }
            if (star.X >= 0)
            {
                starTimer -= dt;
                if (starTimer <= 0) star = (-1, -1);
            }
            if (slow.X >= 0)
            {
                slowTimer -= dt;
                if (slowTimer <= 0) slow = (-1, -1);
            }
            if (doubleP.X >= 0)
            {
                doubleTimer -= dt;
                if (doubleTimer <= 0) doubleP = (-1, -1);
            }

            // 障碍物逐个生成（先预警后落地）
            if (obstacleLevel != ObstacleLevel.Off && obstacles.Count < MaxObstacles)
            {
                obstacleSpawnTimer -= dt;
                if (obstacleSpawnTimer <= 0)
                {
                    SpawnWarning();
                    obstacleSpawnTimer = SpawnInterval + Random.Shared.Next(-1500, 2500);
                }
            }

            // 预警倒计时 → 变成实体障碍
            for (int i = warnings.Count - 1; i >= 0; i--)
            {
                var w = warnings[i];
                w.Timer -= dt;
                if (w.Timer <= 0)
                {
                    if (!cells.Contains((w.X, w.Y)))
                    {
                        obstacles.Add((w.X, w.Y));
                        Burst(Center((w.X, w.Y)),
                            new[] { Color.FromArgb(255, 80, 80), Color.FromArgb(120, 60, 60), Color.White },
                            7, 40f, 130f, 1.5f, 3f, 280, 550);
                    }
                    warnings.RemoveAt(i);
                }
                else warnings[i] = w;
            }

            // 加速时尾部拖出速度线粒子
            if (boostActive > 0 && cells.Count > 0 && Random.Shared.NextDouble() < 0.45)
            {
                var h = cells[0];
                PointF c = Center(h);
                parts.Add(new Particle
                {
                    X = c.X - dir.dx * 14,
                    Y = c.Y - dir.dy * 14,
                    Vx = -dir.dx * 90 + (float)(Random.Shared.NextDouble() * 40 - 20),
                    Vy = -dir.dy * 90 + (float)(Random.Shared.NextDouble() * 40 - 20),
                    R = 1.6f, Life = 280, Age = 0,
                    C = BoostCol
                });
            }

            // 无敌时金色粒子环绕
            if (invincible > 0 && cells.Count > 0 && Random.Shared.NextDouble() < 0.35)
            {
                PointF c = Center(cells[0]);
                float a = (float)(Random.Shared.NextDouble() * Math.PI * 2);
                parts.Add(new Particle
                {
                    X = c.X + MathF.Cos(a) * 16,
                    Y = c.Y + MathF.Sin(a) * 16,
                    Vx = MathF.Cos(a) * 50,
                    Vy = MathF.Sin(a) * 50,
                    R = 1.8f, Life = 350, Age = 0,
                    C = Color.FromArgb(255, 220, 80)
                });
            }

            int guard = 0;
            while (acc >= EffectiveInterval && guard++ < 4 && state == GameState.Playing)
            {
                acc -= EffectiveInterval;
                Step();
            }
            ChaseBody(dt);

            // 蛇头轨迹残影
            if (body.Count > 0)
            {
                trail.Add(body[0]);
                if (trail.Count > 10) trail.RemoveAt(0);
            }
        }

        for (int i = 0; i < parts.Count; i++) parts[i].Step(dt);
        parts.RemoveAll(p => p.Age >= p.Life);

        // 死亡动画：逐节爆炸
        if (deathAnim >= 0 && deathAnim < 1f)
        {
            deathAnim += dt / 900f; // 0.9秒播完
            int n = cells.Count;
            int aliveCount = (int)MathF.Max(0, n * (1f - deathAnim * 1.15f));
            // 尾部消失的节生成爆炸粒子
            for (int i = n - 1; i >= aliveCount; i--)
            {
                if (i < body.Count)
                {
                    Color c = i == 0 ? CurrentHead : BodyColor(i);
                    Burst(body[i], new[] { Color.FromArgb(255, 70, 70), c, Color.White },
                        i == 0 ? 12 : 4, 50f, 200f, 2f, 4f, 400, 1100);
                }
            }
            if (deathAnim >= 1f)
            {
                deathAnim = 1f;
                body.Clear();
            }
        }

        // 飘字更新
        for (int i = floats.Count - 1; i >= 0; i--)
        {
            var f = floats[i];
            f.Age += dt;
            f.Y += f.Vy * dt / 1000f;
            f.Alpha = MathF.Max(0, 1f - f.Age / f.Life);
            if (f.Age >= f.Life) floats.RemoveAt(i);
            else floats[i] = f;
        }

        // 转场
        if (transitionDir != 0)
        {
            transition += transitionDir * dt / 500f; // 0.5秒
            if (transition <= 0) { transition = 0; transitionDir = 0; }
            if (transition >= 1) { transition = 1; transitionDir = 0; }
        }

        // 升级提示计时
        if (levelUpTimer > 0) levelUpTimer -= dt;

        // 食物/道具弹出动画
        if (foodSpawnAnim < 1) foodSpawnAnim = MathF.Min(1, foodSpawnAnim + dt / 250f);
        if (bonusSpawnAnim < 1) bonusSpawnAnim = MathF.Min(1, bonusSpawnAnim + dt / 250f);
        if (boostSpawnAnim < 1) boostSpawnAnim = MathF.Min(1, boostSpawnAnim + dt / 250f);
        if (starSpawnAnim < 1) starSpawnAnim = MathF.Min(1, starSpawnAnim + dt / 250f);
        if (slowSpawnAnim < 1) slowSpawnAnim = MathF.Min(1, slowSpawnAnim + dt / 250f);
        if (doubleSpawnAnim < 1) doubleSpawnAnim = MathF.Min(1, doubleSpawnAnim + dt / 250f);

        // 屏幕闪光衰减
        if (flashAlpha > 0) flashAlpha = MathF.Max(0, flashAlpha - dt / 300f);

        // 冲击波
        for (int i = shockwaves.Count - 1; i >= 0; i--)
        {
            var sw = shockwaves[i];
            sw.Age += dt;
            float t = (float)sw.Age / sw.Life;
            sw.R = 5 + (sw.MaxR - 5) * t;
            sw.Alpha = (1f - t) * 0.6f;
            if (sw.Age >= sw.Life) shockwaves.RemoveAt(i);
            else shockwaves[i] = sw;
        }

        Invalidate();
    }

    void ChaseBody(int dtMs)
    {
        for (int i = 0; i < body.Count; i++)
        {
            PointF t = Center(cells[i]);
            float dx = t.X - body[i].X;
            float dy = t.Y - body[i].Y;
            if (MathF.Abs(dx) > Cell * 5 || MathF.Abs(dy) > Cell * 5)
            {
                body[i] = t;
                continue;
            }
            float tau = i == 0 ? 22f : 26f + i * 2f;
            float f = 1f - MathF.Exp(-dtMs / tau);
            body[i] = new PointF(body[i].X + dx * f, body[i].Y + dy * f);
        }
    }

    void Step()
    {
        if (dirQueue.Count > 0) dir = dirQueue.Dequeue();
        var h = cells[0];
        var nh = (X: h.X + dir.dx, Y: h.Y + dir.dy);

        if (wallMode == WallMode.Walls)
        {
            if (nh.X < 0 || nh.X >= Cols || nh.Y < 0 || nh.Y >= Rows)
            {
                Die();
                return;
            }
        }
        else
        {
            nh.X = (nh.X + Cols) % Cols;
            nh.Y = (nh.Y + Rows) % Rows;
        }

        // 撞障碍物
        if (obstacles.Contains(nh))
        {
            if (invincible > 0)
            {
                obstacles.Remove(nh);
                score += 1;
                Burst(Center(nh),
                    new[] { Color.FromArgb(255, 200, 60), Color.FromArgb(255, 80, 80), Color.White },
                    18, 100f, 320f, 2f, 4.5f, 380, 750);
                Play(sndBreak);
            }
            else
            {
                Die();
                return;
            }
        }

        bool grow = (nh.X, nh.Y) == food;
        bool eatBonus = (nh.X, nh.Y) == bonus;
        bool eatBoost = (nh.X, nh.Y) == speedBoost;
        bool eatStar = (nh.X, nh.Y) == star;
        bool eatSlow = (nh.X, nh.Y) == slow;
        bool eatDouble = (nh.X, nh.Y) == doubleP;

        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] == nh && (grow || eatBonus || eatBoost || eatStar || eatSlow || eatDouble || i < cells.Count - 1))
            {
                Die();
                return;
            }
        }

        cells.Insert(0, (nh.X, nh.Y));

        if (grow)
        {
            PointF eaten = Center(food);
            if (comboTimer > 0) combo = Math.Min(5, combo + 1);
            else combo = 1;
            comboTimer = 4000;
            score += 1 * combo * ScoreMult;
            var (_, step, minI) = DifficultySettings;
            baseInterval = Math.Max(minI, baseInterval - step);
            Burst(eaten, new[] { FoodCol, Color.White, Neon }, 14, 200f, 460f, 2.2f, 4.5f, 400, 900);
            body.Add(body[^1]);
            Play(sndEat);
            flashAlpha = 0.08f;
            flashColor = FoodCol;
            shockwaves.Add(new Shockwave { X = eaten.X, Y = eaten.Y, R = 5, MaxR = 50, Alpha = 0.5f, Width = 2f, Col = FoodCol, Age = 0, Life = 400 });

            // 连击飘字
            if (combo > 1)
                floats.Add(new FloatingText
                {
                    Text = $"COMBO x{combo}",
                    X = eaten.X, Y = eaten.Y - 10,
                    Vy = -40f, Alpha = 1f,
                    Col = Color.FromArgb(255, 230, 120),
                    Size = 1.2f, Life = 900
                });

            // 等级升级（每10个食物）
            int foodsEaten = cells.Count - 3; // 初始3节
            int newLevel = foodsEaten / 10 + 1;
            if (newLevel > level)
            {
                level = newLevel;
                levelUpText = $"LEVEL {level}!";
                levelUpTimer = 1800;
                Play(sndLevelUp);
                flashAlpha = 0.2f;
                flashColor = Color.FromArgb(120, 255, 180);
                PointF lc = new(WidthL / 2f, HeightL / 2f);
                shockwaves.Add(new Shockwave { X = lc.X, Y = lc.Y, R = 10, MaxR = 200, Alpha = 0.7f, Width = 3f, Col = Color.FromArgb(120, 255, 180), Age = 0, Life = 700 });
                shockwaves.Add(new Shockwave { X = lc.X, Y = lc.Y, R = 5, MaxR = 140, Alpha = 0.5f, Width = 2f, Col = Color.White, Age = 0, Life = 500 });
                Burst(lc, new[] { Color.FromArgb(120, 255, 180), Color.White, Neon }, 30, 80f, 300f, 2f, 4f, 500, 1200);
                floats.Add(new FloatingText
                {
                    Text = levelUpText,
                    X = WidthL / 2f, Y = HeightL / 2f - 40,
                    Vy = -20f, Alpha = 1f,
                    Col = Color.FromArgb(120, 255, 180),
                    Size = 2.0f, Life = 1500
                });
            }

            food = NewFood();
            foodSpawnAnim = 0;

            // 生成特殊道具
            if (bonus.X < 0 && speedBoost.X < 0 && star.X < 0 && slow.X < 0 && doubleP.X < 0
                && Random.Shared.NextDouble() < 0.26)
            {
                double r = Random.Shared.NextDouble();
                if (r < 0.22) { bonus = NewFood(); bonusTimer = 6000; bonusSpawnAnim = 0; }
                else if (r < 0.42) { speedBoost = NewFood(); speedBoostTimer = 8000; boostSpawnAnim = 0; }
                else if (r < 0.60) { star = NewFood(); starTimer = 7000; starSpawnAnim = 0; }
                else if (r < 0.80) { slow = NewFood(); slowTimer = 7000; slowSpawnAnim = 0; }
                else { doubleP = NewFood(); doubleTimer = 7000; doubleSpawnAnim = 0; }
            }

            if (food.X < 0)
            {
                state = GameState.Won;
                Burst(Center(cells[0]), new[] { FoodCol, Color.White, Neon, CurrentHead }, 90, 120f, 340f, 1.6f, 4f, 500, 1600);
                CheckHighScore();
            }
        }
        else if (eatBonus)
        {
            PointF eaten = Center(bonus);
            score += 5 * combo * ScoreMult;
            combo = Math.Min(5, combo + 1);
            comboTimer = 4000;
            Burst(eaten, new[] { BonusCol, Color.White, Neon }, 24, 180f, 420f, 2.5f, 5f, 500, 1100);
            Play(sndBonus);
            PickupEffect(eaten, BonusCol);
            floats.Add(new FloatingText { Text = "+5", X = eaten.X, Y = eaten.Y - 8, Vy = -35f, Alpha = 1f, Col = BonusCol, Size = 1.1f, Life = 700 });
            bonus = (-1, -1);
            cells.RemoveAt(cells.Count - 1);
        }
        else if (eatBoost)
        {
            PointF eaten = Center(speedBoost);
            score += 3 * combo * ScoreMult;
            combo = Math.Min(5, combo + 1);
            comboTimer = 4000;
            boostActive = 5000;
            Burst(eaten, new[] { BoostCol, Color.White, Color.FromArgb(255, 130, 30) }, 22, 160f, 400f, 2.4f, 4.8f, 450, 1000);
            Play(sndBoost);
            PickupEffect(eaten, BoostCol);
            floats.Add(new FloatingText { Text = "加速!", X = eaten.X, Y = eaten.Y - 8, Vy = -35f, Alpha = 1f, Col = BoostCol, Size = 1.1f, Life = 700 });
            speedBoost = (-1, -1);
            cells.RemoveAt(cells.Count - 1);
        }
        else if (eatStar)
        {
            PointF eaten = Center(star);
            score += 2 * combo * ScoreMult;
            combo = Math.Min(5, combo + 1);
            comboTimer = 4000;
            invincible = 6000;
            Burst(eaten, new[] { Color.FromArgb(255, 220, 80), Color.White, Color.FromArgb(255, 180, 40) },
                28, 140f, 380f, 2.5f, 5f, 500, 1100);
            Play(sndInvincible);
            PickupEffect(eaten, Color.FromArgb(255, 220, 80));
            floats.Add(new FloatingText { Text = "无敌!", X = eaten.X, Y = eaten.Y - 8, Vy = -35f, Alpha = 1f, Col = Color.FromArgb(255, 220, 80), Size = 1.1f, Life = 700 });
            star = (-1, -1);
            cells.RemoveAt(cells.Count - 1);
        }
        else if (eatSlow)
        {
            PointF eaten = Center(slow);
            score += 2 * combo * ScoreMult;
            combo = Math.Min(5, combo + 1);
            comboTimer = 4000;
            slowActive = 5000;
            Burst(eaten, new[] { Color.FromArgb(100, 200, 255), Color.White, Color.FromArgb(60, 140, 220) },
                22, 140f, 360f, 2.4f, 4.8f, 450, 1000);
            Play(sndSlow);
            PickupEffect(eaten, Color.FromArgb(100, 200, 255));
            floats.Add(new FloatingText { Text = "减速!", X = eaten.X, Y = eaten.Y - 8, Vy = -35f, Alpha = 1f, Col = Color.FromArgb(100, 200, 255), Size = 1.1f, Life = 700 });
            slow = (-1, -1);
            cells.RemoveAt(cells.Count - 1);
        }
        else if (eatDouble)
        {
            PointF eaten = Center(doubleP);
            score += 2 * combo * ScoreMult;
            combo = Math.Min(5, combo + 1);
            comboTimer = 4000;
            doubleActive = 5000;
            Burst(eaten, new[] { Color.FromArgb(80, 255, 200), Color.White, Color.FromArgb(40, 200, 160) },
                22, 140f, 360f, 2.4f, 4.8f, 450, 1000);
            Play(sndDouble);
            PickupEffect(eaten, Color.FromArgb(80, 255, 200));
            floats.Add(new FloatingText { Text = "双倍!", X = eaten.X, Y = eaten.Y - 8, Vy = -35f, Alpha = 1f, Col = Color.FromArgb(80, 255, 200), Size = 1.1f, Life = 700 });
            doubleP = (-1, -1);
            cells.RemoveAt(cells.Count - 1);
        }
        else
        {
            cells.RemoveAt(cells.Count - 1);
        }
    }

    void Die()
    {
        state = GameState.Dead;
        acc = 0;
        boostActive = 0;
        invincible = 0;
        slowActive = 0;
        doubleActive = 0;
        shakeMag = 8f;
        shakeTime = 500;
        Play(sndDie);
        CheckHighScore();
        deathAnim = 0f; // 启动死亡动画
        flashAlpha = 0.3f;
        flashColor = Color.FromArgb(255, 60, 60);
        StopBgm();
    }

    (int X, int Y) NewFood()
    {
        int occupied = cells.Count + obstacles.Count + warnings.Count;
        if (occupied >= Cols * Rows) return (-1, -1);
        for (int tries = 0; tries < 500; tries++)
        {
            var c = (X: Random.Shared.Next(Cols), Y: Random.Shared.Next(Rows));
            if (!cells.Contains(c) && c != bonus && c != speedBoost && c != star && c != slow && c != doubleP
                && !obstacles.Contains(c) && !warnings.Any(w => w.X == c.X && w.Y == c.Y))
                return c;
        }
        for (int x = 0; x < Cols; x++)
            for (int y = 0; y < Rows; y++)
                if (!cells.Contains((x, y)) && (x, y) != bonus && (x, y) != speedBoost && (x, y) != star
                    && (x, y) != slow && (x, y) != doubleP
                    && !obstacles.Contains((x, y)) && !warnings.Any(w => w.X == x && w.Y == y))
                    return (x, y);
        return (-1, -1);
    }

    void Burst(PointF at, Color[] colors, int count, float minSp, float maxSp,
        float minR, float maxR, float minLife, float maxLife)
    {
        for (int i = 0; i < count; i++)
        {
            float a = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            float sp = minSp + (float)Random.Shared.NextDouble() * (maxSp - minSp);
            parts.Add(new Particle
            {
                X = at.X, Y = at.Y,
                Vx = MathF.Cos(a) * sp, Vy = MathF.Sin(a) * sp,
                R = minR + (float)Random.Shared.NextDouble() * (maxR - minR),
                Life = minLife + (float)Random.Shared.NextDouble() * (maxLife - minLife),
                C = colors[Random.Shared.Next(colors.Length)]
            });
        }
    }

    void PickupEffect(PointF p, Color col)
    {
        flashAlpha = 0.1f;
        flashColor = col;
        shockwaves.Add(new Shockwave { X = p.X, Y = p.Y, R = 5, MaxR = 65, Alpha = 0.55f, Width = 2.5f, Col = col, Age = 0, Life = 450 });
    }

    // ── 蛇身颜色渐变 & 半径 ───────────────────────────────────
    Color BodyColor(int i)
    {
        if (i == 0) return CurrentHead;
        int n = body.Count;
        var pal = CurrentPal;
        if (n <= 2) return pal[0];
        // 颜色沿身体流动
        float flow = (gameT * 0.00025f) % 1f;
        float t = ((float)(i - 1) / (n - 2) + flow) % 1f;
        float pos = t * (pal.Length - 1);
        int idx = (int)pos;
        float frac = pos - idx;
        if (idx >= pal.Length - 1) return pal[^1];
        return LerpColor(pal[idx], pal[idx + 1], frac);
    }

    static Color LerpColor(Color a, Color b, float t) =>
        Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));

    float BodyRadius(int i, int n)
    {
        float baseR;
        if (i == 0) baseR = 13.5f;
        else
        {
            float t = (float)(i - 1) / MathF.Max(1, n - 2);
            baseR = 12.5f - t * 6.5f;
            baseR = MathF.Max(3f, baseR);
        }
        float wiggle = MathF.Sin(gameT * 0.008f + i * 0.45f) * 0.6f;
        return baseR + wiggle;
    }

    SolidBrush Brush(Color c)
    {
        if (!brushCache.TryGetValue(c.ToArgb(), out var b))
        {
            b = new SolidBrush(c);
            brushCache[c.ToArgb()] = b;
        }
        return b;
    }

    // ── 渲染 ──────────────────────────────────────────────────
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        float k = DeviceDpi / 96f;
        float gameW = WidthL * k;
        float gameH = HeightL * k;
        float s = MathF.Min(ClientSize.Width / gameW, ClientSize.Height / gameH);
        float ox = (ClientSize.Width - gameW * s) / 2f;
        float oy = (ClientSize.Height - gameH * s) / 2f;
        g.TranslateTransform(ox, oy);
        g.ScaleTransform(k * s, k * s);

        g.Clear(Bg);
        DrawAmbient(g);

        RectangleF play = new(Margin - 3, BoardTop - 3, Cols * Cell + 6, Rows * Cell + 6);

        var gs = g.Save();
        g.TranslateTransform(shakeX, shakeY);

        if (state == GameState.Menu)
        {
            DrawMenu(g, play);
            g.Restore(gs);
            return;
        }

        DrawGameBackground(g, play);
        DrawBoard(g, play);

        DrawObstacles(g);
        DrawWarnings(g);

        if (food.X >= 0 && state != GameState.Won) DrawFood(g);
        if (bonus.X >= 0) DrawBonus(g);
        if (speedBoost.X >= 0) DrawSpeedBoost(g);
        if (star.X >= 0) DrawStar(g);
        if (slow.X >= 0) DrawSlow(g);
        if (doubleP.X >= 0) DrawDouble(g);
        // 蛇头轨迹残影
        for (int i = 0; i < trail.Count; i++)
        {
            float a = (float)(i + 1) / trail.Count * 0.15f;
            float r = 8f * (float)(i + 1) / trail.Count;
            g.FillEllipse(Brush(Color.FromArgb((int)(a * 255), CurrentHead)),
                trail[i].X - r, trail[i].Y - r, r * 2, r * 2);
        }

        if (deathAnim < 1f) DrawSnake(g);

        foreach (var p in parts)
        {
            float fade = 1f - p.Age / p.Life;
            g.FillEllipse(Brush(Color.FromArgb((int)(p.C.A * fade), p.C)),
                p.X - p.R, p.Y - p.R, p.R * 2, p.R * 2);
        }

        // 冲击波
        foreach (var sw in shockwaves)
        {
            using var pen = new Pen(Color.FromArgb((int)(sw.Alpha * 255), sw.Col), sw.Width);
            g.DrawEllipse(pen, sw.X - sw.R, sw.Y - sw.R, sw.R * 2, sw.R * 2);
        }

        // 飘字
        foreach (var ft in floats)
        {
            using var ffont = new Font("Microsoft YaHei UI", 7f * DeviceDpi / 96f * ft.Size, FontStyle.Bold);
            SizeF fsz = g.MeasureString(ft.Text, ffont);
            g.DrawString(ft.Text, ffont,
                Brush(Color.FromArgb((int)(ft.Alpha * 255), ft.Col)),
                ft.X - fsz.Width / 2, ft.Y - fsz.Height / 2);
        }

        g.Restore(gs);

        DrawHud(g);

        if (state == GameState.Dead && deathAnim >= 1f) DrawDeadOverlay(g, play);
        else if (state == GameState.Won) DrawWonOverlay(g, play);
        else if (state == GameState.Paused) DrawPausedOverlay(g, play);

        // 屏幕闪光
        if (flashAlpha > 0)
            g.FillRectangle(Brush(Color.FromArgb((int)(flashAlpha * 255), flashColor)), 0, 0, Width, Height);

        // 高连击时屏幕边缘脉冲光
        if (combo >= 3 && state == GameState.Playing)
        {
            float pulse = 0.5f + 0.5f * MathF.Sin(gameT * 0.01f);
            int edgeA = (int)(15 + combo * 4 * pulse);
            Color edgeCol = combo >= 5 ? Color.FromArgb(255, 80, 80) : Color.FromArgb(255, 180, 60);
            // 四条边
            g.FillRectangle(Brush(Color.FromArgb(edgeA, edgeCol)), 0, 0, Width, 6);
            g.FillRectangle(Brush(Color.FromArgb(edgeA, edgeCol)), 0, Height - 6, Width, 6);
            g.FillRectangle(Brush(Color.FromArgb(edgeA, edgeCol)), 0, 0, 6, Height);
            g.FillRectangle(Brush(Color.FromArgb(edgeA, edgeCol)), Width - 6, 0, 6, Height);
        }

        // 转场覆盖
        if (transition > 0)
            g.FillRectangle(Brush(Color.FromArgb((int)(transition * 255), Color.Black)), 0, 0, Width, Height);
    }

    void DrawAmbient(Graphics g)
    {
        foreach (var a in ambient)
            g.FillEllipse(Brush(Color.FromArgb((int)a.Alpha, 100, 160, 220)),
                a.X - a.R, a.Y - a.R, a.R * 2, a.R * 2);
    }

    void DrawBoard(Graphics g, RectangleF play)
    {
        // 场地深色底
        FillRound(g, play, 12f, Brush(Color.FromArgb(14, 17, 23)));

        // 发光网格线
        using var gridFine = new Pen(Color.FromArgb(16, 70, 130, 180), 0.5f);
        using var gridBold = new Pen(Color.FromArgb(32, 80, 170, 220), 0.8f);
        float left = Margin, top = BoardTop;
        float right = left + Cols * Cell, bottom = top + Rows * Cell;
        for (int x = 0; x <= Cols; x++)
        {
            float lx = left + x * Cell;
            g.DrawLine(x % 5 == 0 ? gridBold : gridFine, lx, top, lx, bottom);
        }
        for (int y = 0; y <= Rows; y++)
        {
            float ly = top + y * Cell;
            g.DrawLine(y % 5 == 0 ? gridBold : gridFine, left, ly, right, ly);
        }

        // 网格交点微光
        for (int x = 0; x <= Cols; x += 5)
            for (int y = 0; y <= Rows; y += 5)
                g.FillEllipse(Brush(Color.FromArgb(25, 100, 200, 255)),
                    left + x * Cell - 1.5f, top + y * Cell - 1.5f, 3f, 3f);

        // 多层外发光
        FillRound(g, RectangleF.Inflate(play, 8, 8), 15f, Brush(Color.FromArgb(18, 60, 180, 255)));
        FillRound(g, RectangleF.Inflate(play, 4, 4), 13f, Brush(Color.FromArgb(28, 80, 230, 255)));
        using var pen = new Pen(Color.FromArgb(210, Neon), 2f);
        DrawRound(g, play, 12f, pen);
    }

    void DrawGameBackground(Graphics g, RectangleF play)
    {
        // 径向渐变光晕（中心偏亮）
        float cx = play.X + play.Width / 2;
        float cy = play.Y + play.Height / 2;
        for (int i = 0; i < 3; i++)
        {
            float ox = cx + MathF.Sin(gameT * 0.00025f + i * 2.1f) * 160;
            float oy = cy + MathF.Cos(gameT * 0.0002f + i * 1.7f) * 110;
            Color gc = i == 0
                ? Color.FromArgb(16, 0, 140, 220)
                : i == 1
                    ? Color.FromArgb(12, 140, 0, 180)
                    : Color.FromArgb(10, 220, 100, 0);
            g.FillEllipse(Brush(gc), ox - 140, oy - 140, 280, 280);
        }

        // 缓慢漂移的装饰圆环
        for (int i = 0; i < 2; i++)
        {
            float rx = cx + MathF.Cos(gameT * 0.00015f + i * 3f) * 200;
            float ry = cy + MathF.Sin(gameT * 0.00018f + i * 2.5f) * 130;
            float rr = 50 + 20 * MathF.Sin(gameT * 0.0004f + i);
            using var ring = new Pen(Color.FromArgb(14, 100, 180, 240), 1f);
            g.DrawEllipse(ring, rx - rr, ry - rr, rr * 2, rr * 2);
        }
    }

    void DrawObstacles(Graphics g)
    {
        foreach (var o in obstacles)
        {
            PointF c = Center(o);
            float s = Cell * 0.8f;
            float x = c.X - s / 2, y = c.Y - s / 2;
            var rect = new RectangleF(x, y, s, s);

            // 危险光晕
            g.FillRectangle(Brush(Color.FromArgb(18, 255, 60, 60)), x - 3, y - 3, s + 6, s + 6);
            // 石块主体
            FillRound(g, rect, 4f, Brush(Color.FromArgb(68, 62, 56)));
            // 顶部高光
            g.FillRectangle(Brush(Color.FromArgb(35, Color.White)), x + 2, y + 2, s - 4, 3);
            // 底部阴影
            g.FillRectangle(Brush(Color.FromArgb(40, Color.Black)), x + 2, y + s - 5, s - 4, 3);
            // 裂纹
            using var crack = new Pen(Color.FromArgb(70, 30, 30), 1f);
            g.DrawLine(crack, x + s * 0.25f, y + s * 0.3f, x + s * 0.5f, y + s * 0.65f);
            g.DrawLine(crack, x + s * 0.6f, y + s * 0.2f, x + s * 0.78f, y + s * 0.55f);
            // 红色边缘
            using var edge = new Pen(Color.FromArgb(130, 255, 80, 80), 1.2f);
            DrawRound(g, rect, 4f, edge);
        }
    }

    void DrawFood(Graphics g)
    {
        PointF c = Center(food);
        float sp = SpawnScale(foodSpawnAnim);
        float pulse = (1f + 0.22f * MathF.Sin(gameT * 0.007f)) * sp;
        g.FillEllipse(Brush(Color.FromArgb(26, FoodCol)), c.X - 17 * pulse, c.Y - 17 * pulse, 34 * pulse, 34 * pulse);
        g.FillEllipse(Brush(Color.FromArgb(150, FoodCol)), c.X - 8 * pulse, c.Y - 8 * pulse, 16 * pulse, 16 * pulse);
        g.FillEllipse(Brush(Color.FromArgb(230, Color.White)), c.X - 3.4f * pulse, c.Y - 3.4f * pulse, 6.8f * pulse, 6.8f * pulse);
    }

    void DrawBonus(Graphics g)
    {
        PointF c = Center(bonus);
        float sp = SpawnScale(bonusSpawnAnim);
        float pulse = (1f + 0.35f * MathF.Sin(gameT * 0.012f)) * sp;
        float blink = bonusTimer < 1500 ? (MathF.Sin(gameT * 0.03f) > 0 ? 1f : 0.35f) : 1f;
        using var ring = new Pen(Color.FromArgb((int)(180 * blink), BonusCol), 2f);
        g.DrawEllipse(ring, c.X - 14 * pulse, c.Y - 14 * pulse, 28 * pulse, 28 * pulse);
        g.FillEllipse(Brush(Color.FromArgb((int)(40 * blink), BonusCol)), c.X - 16 * pulse, c.Y - 16 * pulse, 32 * pulse, 32 * pulse);
        g.FillEllipse(Brush(Color.FromArgb((int)(220 * blink), BonusCol)), c.X - 7 * pulse, c.Y - 7 * pulse, 14 * pulse, 14 * pulse);
        g.FillEllipse(Brush(Color.FromArgb((int)(240 * blink), Color.White)), c.X - 2.8f * pulse, c.Y - 2.8f * pulse, 5.6f * pulse, 5.6f * pulse);
    }

    void DrawSpeedBoost(Graphics g)
    {
        PointF c = Center(speedBoost);
        float sp = SpawnScale(boostSpawnAnim);
        float pulse = (1f + 0.3f * MathF.Sin(gameT * 0.015f)) * sp;
        float blink = speedBoostTimer < 1500 ? (MathF.Sin(gameT * 0.03f) > 0 ? 1f : 0.35f) : 1f;

        g.FillEllipse(Brush(Color.FromArgb((int)(35 * blink), BoostCol)),
            c.X - 16 * pulse, c.Y - 16 * pulse, 32 * pulse, 32 * pulse);

        float s = 9.5f * pulse;
        var bolt = new[]
        {
            new PointF(c.X + 2.5f, c.Y - s),
            new PointF(c.X - s * 0.65f, c.Y + 1.5f),
            new PointF(c.X - 0.5f, c.Y + 1.5f),
            new PointF(c.X - 2.5f, c.Y + s),
            new PointF(c.X + s * 0.65f, c.Y - 1.5f),
            new PointF(c.X + 0.5f, c.Y - 1.5f),
        };
        g.FillPolygon(Brush(Color.FromArgb((int)(245 * blink), BoostCol)), bolt);
        g.FillPolygon(Brush(Color.FromArgb((int)(180 * blink), Color.White)), new[]
        {
            new PointF(c.X + 1, c.Y - s * 0.55f),
            new PointF(c.X - s * 0.28f, c.Y + 0.5f),
            new PointF(c.X, c.Y + 0.5f),
            new PointF(c.X - 1, c.Y + s * 0.5f),
        });
    }

    void DrawWarnings(Graphics g)
    {
        foreach (var w in warnings)
        {
            PointF c = Center((w.X, w.Y));
            float s = Cell * 0.82f;
            float x = c.X - s / 2, y = c.Y - s / 2;
            // 闪烁频率随倒计时加快
            float freq = w.Timer < 600 ? 0.04f : 0.018f;
            float blink = MathF.Sin(gameT * freq) > 0 ? 1f : 0.35f;
            // 红色半透明底
            FillRound(g, new RectangleF(x, y, s, s), 4f,
                Brush(Color.FromArgb((int)(70 * blink), 255, 40, 40)));
            // 闪烁边框
            using var edge = new Pen(Color.FromArgb((int)(220 * blink), 255, 60, 60), 2f);
            DrawRound(g, new RectangleF(x, y, s, s), 4f, edge);
            // 感叹号
            string ex = "!";
            SizeF es = g.MeasureString(ex, fBig);
            g.DrawString(ex, fBig, Brush(Color.FromArgb((int)(255 * blink), 255, 80, 80)),
                c.X - es.Width / 2, c.Y - es.Height / 2 - 1);
        }
    }

    void DrawStar(Graphics g)
    {
        PointF c = Center(star);
        float sp = SpawnScale(starSpawnAnim);
        float pulse = (1f + 0.25f * MathF.Sin(gameT * 0.01f)) * sp;
        float blink = starTimer < 1500 ? (MathF.Sin(gameT * 0.03f) > 0 ? 1f : 0.4f) : 1f;
        float rot = gameT * 0.0015f;
        float r = 11f * pulse;

        // 光晕
        g.FillEllipse(Brush(Color.FromArgb((int)(45 * blink), 255, 220, 80)),
            c.X - r * 1.7f, c.Y - r * 1.7f, r * 3.4f, r * 3.4f);

        // 五角星（10 个顶点，内外交替）
        var pts = new PointF[10];
        for (int i = 0; i < 10; i++)
        {
            float ang = -MathF.PI / 2 + rot + i * MathF.PI / 5f;
            float rad = i % 2 == 0 ? r : r * 0.45f;
            pts[i] = new PointF(c.X + MathF.Cos(ang) * rad, c.Y + MathF.Sin(ang) * rad);
        }
        g.FillPolygon(Brush(Color.FromArgb((int)(245 * blink), 255, 215, 60)), pts);
        // 内圈高光
        var inner = new PointF[10];
        for (int i = 0; i < 10; i++)
        {
            float ang = -MathF.PI / 2 + rot + i * MathF.PI / 5f;
            float rad = i % 2 == 0 ? r * 0.5f : r * 0.22f;
            inner[i] = new PointF(c.X + MathF.Cos(ang) * rad, c.Y + MathF.Sin(ang) * rad);
        }
        g.FillPolygon(Brush(Color.FromArgb((int)(170 * blink), Color.White)), inner);
    }

    void DrawSlow(Graphics g)
    {
        PointF c = Center(slow);
        float sp = SpawnScale(slowSpawnAnim);
        float pulse = (1f + 0.25f * MathF.Sin(gameT * 0.01f)) * sp;
        float blink = slowTimer < 1500 ? (MathF.Sin(gameT * 0.03f) > 0 ? 1f : 0.4f) : 1f;
        Color col = Color.FromArgb(100, 200, 255);
        float r = 10f * pulse;

        g.FillEllipse(Brush(Color.FromArgb((int)(35 * blink), col)),
            c.X - r * 1.7f, c.Y - r * 1.7f, r * 3.4f, r * 3.4f);

        // 雪花：6 根放射线
        using var pen = new Pen(Color.FromArgb((int)(230 * blink), col), 2f);
        for (int i = 0; i < 6; i++)
        {
            float ang = i * MathF.PI / 3f + gameT * 0.001f;
            g.DrawLine(pen, c.X, c.Y,
                c.X + MathF.Cos(ang) * r, c.Y + MathF.Sin(ang) * r);
            // 小分叉
            float mx = c.X + MathF.Cos(ang) * r * 0.6f;
            float my = c.Y + MathF.Sin(ang) * r * 0.6f;
            g.DrawLine(pen, mx, my,
                mx + MathF.Cos(ang + 0.5f) * r * 0.35f, my + MathF.Sin(ang + 0.5f) * r * 0.35f);
            g.DrawLine(pen, mx, my,
                mx + MathF.Cos(ang - 0.5f) * r * 0.35f, my + MathF.Sin(ang - 0.5f) * r * 0.35f);
        }
        g.FillEllipse(Brush(Color.FromArgb((int)(200 * blink), Color.White)), c.X - 2.5f, c.Y - 2.5f, 5f, 5f);
    }

    void DrawDouble(Graphics g)
    {
        PointF c = Center(doubleP);
        float sp = SpawnScale(doubleSpawnAnim);
        float pulse = (1f + 0.25f * MathF.Sin(gameT * 0.011f)) * sp;
        float blink = doubleTimer < 1500 ? (MathF.Sin(gameT * 0.03f) > 0 ? 1f : 0.4f) : 1f;
        Color col = Color.FromArgb(80, 255, 200);
        float r = 11f * pulse;

        g.FillEllipse(Brush(Color.FromArgb((int)(35 * blink), col)),
            c.X - r * 1.6f, c.Y - r * 1.6f, r * 3.2f, r * 3.2f);

        // 菱形外框
        var diamond = new[]
        {
            new PointF(c.X, c.Y - r),
            new PointF(c.X + r, c.Y),
            new PointF(c.X, c.Y + r),
            new PointF(c.X - r, c.Y),
        };
        g.FillPolygon(Brush(Color.FromArgb((int)(50 * blink), col)), diamond);
        using var pen = new Pen(Color.FromArgb((int)(220 * blink), col), 2f);
        g.DrawPolygon(pen, diamond);

        // "x2" 文字
        string txt = "x2";
        SizeF tsz = g.MeasureString(txt, fMid);
        g.DrawString(txt, fMid, Brush(Color.FromArgb((int)(255 * blink), Color.White)),
            c.X - tsz.Width / 2, c.Y - tsz.Height / 2 - 1);
    }

    void DrawSnake(Graphics g)
    {
        int n = body.Count;
        if (n == 0 || cells.Count != n) return;

        // 死亡动画：只画存活的前 N 节
        int drawN = n;
        if (deathAnim >= 0 && deathAnim < 1f)
            drawN = (int)MathF.Max(1, n * (1f - deathAnim * 1.15f));

        // 加速时外圈光晕
        if (boostActive > 0)
        {
            float a = 0.3f + 0.15f * MathF.Sin(gameT * 0.02f);
            for (int i = 0; i < drawN; i++)
            {
                PointF p = body[i];
                float r = BodyRadius(i, n);
                g.FillEllipse(Brush(Color.FromArgb((int)(a * 80), BoostCol)),
                    p.X - r * 1.9f, p.Y - r * 1.9f, r * 3.8f, r * 3.8f);
            }
        }

        // 无敌时金色光晕
        if (invincible > 0)
        {
            float a = 0.4f + 0.2f * MathF.Sin(gameT * 0.025f);
            for (int i = 0; i < drawN; i++)
            {
                PointF p = body[i];
                float r = BodyRadius(i, n);
                g.FillEllipse(Brush(Color.FromArgb((int)(a * 90), 255, 215, 60)),
                    p.X - r * 2.1f, p.Y - r * 2.1f, r * 4.2f, r * 4.2f);
            }
        }

        // 减速时蓝色光晕
        if (slowActive > 0)
        {
            float a = 0.3f + 0.12f * MathF.Sin(gameT * 0.018f);
            for (int i = 0; i < drawN; i++)
            {
                PointF p = body[i];
                float r = BodyRadius(i, n);
                g.FillEllipse(Brush(Color.FromArgb((int)(a * 70), 100, 200, 255)),
                    p.X - r * 1.85f, p.Y - r * 1.85f, r * 3.7f, r * 3.7f);
            }
        }

        // 双倍分时青绿光晕
        if (doubleActive > 0)
        {
            float a = 0.3f + 0.12f * MathF.Sin(gameT * 0.022f);
            for (int i = 0; i < drawN; i++)
            {
                PointF p = body[i];
                float r = BodyRadius(i, n);
                g.FillEllipse(Brush(Color.FromArgb((int)(a * 60), 80, 255, 200)),
                    p.X - r * 1.8f, p.Y - r * 1.8f, r * 3.6f, r * 3.6f);
            }
        }

        // 阴影层（偏移右下，增加立体感）
        for (int i = drawN - 1; i >= 1; i--)
        {
            float r = BodyRadius(i, n);
            using var spen = new Pen(Color.FromArgb(45, 0, 0, 0), r * 2)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(spen, body[i].X + 2.5f, body[i].Y + 3.5f, body[i - 1].X + 2.5f, body[i - 1].Y + 3.5f);
        }

        // 主体身体：圆头连线
        for (int i = drawN - 1; i >= 1; i--)
        {
            Color col = BodyColor(i);
            float r = BodyRadius(i, n);
            using var pen = new Pen(col, r * 2) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, body[i], body[i - 1]);
        }

        // 鳞片分隔线 + 顶部高光
        for (int i = 1; i < drawN; i++)
        {
            PointF p = body[i];
            float r = BodyRadius(i, n);
            PointF prev = body[Math.Max(0, i - 1)];
            PointF next = body[Math.Min(n - 1, i + 1)];
            float ddx = next.X - prev.X, ddy = next.Y - prev.Y;
            float dl = MathF.Sqrt(ddx * ddx + ddy * ddy);
            if (dl < 0.1f) continue;
            ddx /= dl; ddy /= dl;
            float perpX = -ddy, perpY = ddx;

            // 鳞片分隔（每隔一节画一条短弧线）
            if (i % 2 == 0)
            {
                Color scaleDark = Color.FromArgb(45, 0, 0, 0);
                using var scalePen = new Pen(scaleDark, 1.2f);
                float sw = r * 0.85f;
                g.DrawLine(scalePen,
                    p.X + perpX * sw - ddx * r * 0.3f, p.Y + perpY * sw - ddy * r * 0.3f,
                    p.X - perpX * sw - ddx * r * 0.3f, p.Y - perpY * sw - ddy * r * 0.3f);
            }

            // 顶部高光带（沿身体一侧的细亮线）
            if (i < drawN - 1)
            {
                float off = r * 0.3f;
                using var hpen = new Pen(Color.FromArgb(38, Color.White), r * 0.45f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(hpen,
                    p.X + perpX * off, p.Y + perpY * off,
                    body[i + 1].X + perpX * off, body[i + 1].Y + perpY * off);
            }
        }

        // 关节圆（平滑连接处）
        for (int i = drawN - 1; i >= 1; i--)
        {
            PointF p = body[i];
            float r = BodyRadius(i, n);
            g.FillEllipse(Brush(BodyColor(i)), p.X - r, p.Y - r, r * 2, r * 2);
        }

        // ── 蛇头：椭圆 + 鼻孔 + 嘴 + 逼真竖瞳 ──
        PointF h = body[0];
        float bob = MathF.Sin(gameT * 0.012f) * 1.2f;
        float vx = visualDir.vx;
        float vy = visualDir.vy;
        float vlen = MathF.Sqrt(vx * vx + vy * vy);
        if (vlen < 0.1f) { vx = dir.dx; vy = dir.dy; vlen = 1f; }
        vx /= vlen; vy /= vlen;
        float px = -vy, py = vx;
        float headAng = MathF.Atan2(vy, vx) * 180f / MathF.PI;
        float bobX = px * bob, bobY = py * bob;

        // 蛇信子（在头之前画，让头盖住根部）
        if (tongueT > 0)
        {
            float ext = tongueT > 0.5f ? (1f - tongueT) * 2f : tongueT * 2f;
            float tbase = 14f;
            float ttip = tbase + ext * 11f;
            PointF t0 = new(h.X + bobX + vx * (tbase - 2), h.Y + bobY + vy * (tbase - 2));
            PointF t1 = new(h.X + bobX + vx * ttip, h.Y + bobY + vy * ttip);
            using var tpen = new Pen(CurrentTongue, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(tpen, t0, t1);
            g.DrawLine(tpen, t1, new PointF(t1.X + vx * 3.5f + px * 3.5f, t1.Y + vy * 3.5f + py * 3.5f));
            g.DrawLine(tpen, t1, new PointF(t1.X + vx * 3.5f - px * 3.5f, t1.Y + vy * 3.5f - py * 3.5f));
        }

        // 用变换绘制椭圆蛇头
        var hs = g.Save();
        g.TranslateTransform(h.X + bobX, h.Y + bobY);
        g.RotateTransform(headAng);

        float hLen = 17f, hWid = 12.5f;
        // 头部阴影
        g.FillEllipse(Brush(Color.FromArgb(50, 0, 0, 0)), -hLen * 0.7f + 2.5f, -hWid + 3.5f, hLen * 1.4f, hWid * 2);
        // 头部主体（椭圆，沿前进方向拉长）
        g.FillEllipse(Brush(CurrentHead), -hLen * 0.7f, -hWid, hLen * 1.4f, hWid * 2);
        // 头部顶部高光
        g.FillEllipse(Brush(Color.FromArgb(50, Color.White)), -hLen * 0.5f, -hWid * 0.85f, hLen * 0.9f, hWid * 0.8f);
        // 嘴线
        using var mouthPen = new Pen(Color.FromArgb(120, 40, 20, 20), 1.2f);
        g.DrawLine(mouthPen, hLen * 0.1f, 0, hLen * 0.65f, 0);
        // 鼻孔
        g.FillEllipse(Brush(Color.FromArgb(100, 20, 10, 10)), hLen * 0.55f, -3.5f, 2f, 1.6f);
        g.FillEllipse(Brush(Color.FromArgb(100, 20, 10, 10)), hLen * 0.55f, 2f, 2f, 1.6f);

        // 逼真蛇眼：眼眶 + 虹膜 + 竖瞳 + 高光
        DrawRealisticEye(g, -1, hLen, hWid);
        DrawRealisticEye(g, 1, hLen, hWid);

        g.Restore(hs);
    }

    void DrawRealisticEye(Graphics g, float side, float hLen, float hWid)
    {
        float ex = -hLen * 0.05f;
        float ey = side * hWid * 0.62f;
        // 眼眶（深色底）
        g.FillEllipse(Brush(Color.FromArgb(200, 25, 20, 15)), ex - 5.5f, ey - 5.5f, 11f, 11f);
        // 虹膜（金黄）
        g.FillEllipse(Brush(Color.FromArgb(255, 220, 170, 60)), ex - 4.2f, ey - 4.2f, 8.4f, 8.4f);
        // 竖瞳（蛇的竖缝瞳孔）
        g.FillEllipse(Brush(Color.FromArgb(20, 15, 10)), ex - 1.1f, ey - 3.8f, 2.2f, 7.6f);
        // 高光点
        g.FillEllipse(Brush(Color.FromArgb(220, Color.White)), ex - 2.5f, ey - 3f, 2.2f, 2.2f);
    }

    void DrawHud(Graphics g)
    {
        g.FillRectangle(Brush(Color.FromArgb(10, 19, 30)), 0, 0, WidthL, HudH);
        using var pen = new Pen(Color.FromArgb(70, Neon), 1f);
        g.DrawLine(pen, 0, HudH - 1, WidthL, HudH - 1);

        g.DrawString("贪吃蛇 SNAKE", fMid, Brush(Color.White), Margin, 20);

        string scoreS = $"得分 {score}";
        string highS = $"最高 {highScore}";
        string levelS = $"等级 {level}";
        string speedS = $"速度 {EffectiveInterval}ms";
        string lenS = $"长度 {cells.Count}";
        string modeS = wallMode == WallMode.Walls ? "墙壁" : "穿墙";
        string skinS = CurrentSkin.Name;

        float x = Margin + 150;
        const float y = 20;
        g.DrawString(scoreS, fMid, Brush(Color.FromArgb(255, 214, 80)), x, y);
        x += g.MeasureString(scoreS, fMid).Width + 14;
        g.DrawString(highS, fMid, Brush(Color.FromArgb(255, 160, 80)), x, y);
        x += g.MeasureString(highS, fMid).Width + 14;
        g.DrawString(levelS, fMid, Brush(Color.FromArgb(120, 255, 180)), x, y);
        x += g.MeasureString(levelS, fMid).Width + 14;
        g.DrawString(speedS, fMid, Brush(Neon), x, y);
        x += g.MeasureString(speedS, fMid).Width + 14;
        g.DrawString(lenS, fMid, Brush(Color.White), x, y);
        x += g.MeasureString(lenS, fMid).Width + 14;
        g.DrawString(modeS, fMid, Brush(Color.FromArgb(180, 200, 220)), x, y);
        x += g.MeasureString(modeS, fMid).Width + 14;
        g.DrawString(skinS, fMid, Brush(CurrentHead), x, y);

        // 第二行：连击 & 加速状态
        float x2 = Margin + 150;
        if (combo > 1 && state == GameState.Playing)
        {
            float a = MathF.Min(1f, comboTimer / 1000f);
            string cs = $"x{combo} 连击!";
            g.DrawString(cs, fMid,
                Brush(Color.FromArgb((int)(255 * a), 255, 120, 200)), x2, 38);
            x2 += g.MeasureString(cs, fMid).Width + 18;
        }
        if (boostActive > 0)
        {
            string bs = $"加速 {boostActive / 1000f:F1}s";
            g.DrawString(bs, fMid, Brush(BoostCol), x2, 38);
            x2 += g.MeasureString(bs, fMid).Width + 18;
        }
        if (invincible > 0)
        {
            string iv = $"无敌 {invincible / 1000f:F1}s";
            g.DrawString(iv, fMid, Brush(Color.FromArgb(255, 215, 60)), x2, 38);
            x2 += g.MeasureString(iv, fMid).Width + 18;
        }
        if (slowActive > 0)
        {
            string sv = $"减速 {slowActive / 1000f:F1}s";
            g.DrawString(sv, fMid, Brush(Color.FromArgb(100, 200, 255)), x2, 38);
            x2 += g.MeasureString(sv, fMid).Width + 18;
        }
        if (doubleActive > 0)
        {
            string dv = $"双倍 {doubleActive / 1000f:F1}s";
            g.DrawString(dv, fMid, Brush(Color.FromArgb(80, 255, 200)), x2, 38);
        }

        if (muted)
            g.DrawString("静音", fHint, Brush(Color.FromArgb(140, 140, 140)), WidthL - Margin - 36, 38);

        string hint = "空格 暂停  R 重开  M 静音  B 音乐  F11 全屏  Esc 菜单";
        SizeF hs = g.MeasureString(hint, fHint);
        g.DrawString(hint, fHint, Brush(Color.FromArgb(110, 132, 152)), WidthL - Margin - hs.Width, 22);
    }

    // ── 菜单 ──────────────────────────────────────────────────
    void DrawPowerUpIcon(Graphics g, int type, PointF c, float s)
    {
        switch (type)
        {
            case 0: // 奖励分 紫球
                g.FillEllipse(Brush(Color.FromArgb(40, BonusCol)), c.X - s * 1.5f, c.Y - s * 1.5f, s * 3, s * 3);
                g.FillEllipse(Brush(BonusCol), c.X - s * 0.75f, c.Y - s * 0.75f, s * 1.5f, s * 1.5f);
                g.FillEllipse(Brush(Color.FromArgb(200, Color.White)), c.X - s * 0.25f, c.Y - s * 0.25f, s * 0.5f, s * 0.5f);
                break;
            case 1: // 加速 闪电
                {
                    var bolt = new[]
                    {
                        new PointF(c.X + s * 0.25f, c.Y - s),
                        new PointF(c.X - s * 0.65f, c.Y + s * 0.15f),
                        new PointF(c.X - s * 0.05f, c.Y + s * 0.15f),
                        new PointF(c.X - s * 0.25f, c.Y + s),
                        new PointF(c.X + s * 0.65f, c.Y - s * 0.15f),
                        new PointF(c.X + s * 0.05f, c.Y - s * 0.15f),
                    };
                    g.FillPolygon(Brush(BoostCol), bolt);
                }
                break;
            case 2: // 无敌星
                {
                    var pts = new PointF[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float ang = -MathF.PI / 2 + i * MathF.PI / 5f;
                        float rad = i % 2 == 0 ? s : s * 0.45f;
                        pts[i] = new PointF(c.X + MathF.Cos(ang) * rad, c.Y + MathF.Sin(ang) * rad);
                    }
                    g.FillPolygon(Brush(Color.FromArgb(255, 215, 60)), pts);
                }
                break;
            case 3: // 减速 雪花
                {
                    using var pen = new Pen(Color.FromArgb(100, 200, 255), 2f);
                    for (int i = 0; i < 6; i++)
                    {
                        float ang = i * MathF.PI / 3f;
                        g.DrawLine(pen, c.X, c.Y, c.X + MathF.Cos(ang) * s, c.Y + MathF.Sin(ang) * s);
                    }
                    g.FillEllipse(Brush(Color.FromArgb(200, Color.White)), c.X - 2, c.Y - 2, 4, 4);
                }
                break;
            case 4: // 双倍分 菱形x2
                {
                    var diamond = new[]
                    {
                        new PointF(c.X, c.Y - s),
                        new PointF(c.X + s, c.Y),
                        new PointF(c.X, c.Y + s),
                        new PointF(c.X - s, c.Y),
                    };
                    g.FillPolygon(Brush(Color.FromArgb(60, 80, 255, 200)), diamond);
                    using var pen = new Pen(Color.FromArgb(80, 255, 200), 1.5f);
                    g.DrawPolygon(pen, diamond);
                    g.DrawString("x2", fHint, Brush(Color.White), c.X - 8, c.Y - 7);
                }
                break;
        }
    }

    void DrawDecorSnakes(Graphics g)
    {
        foreach (var ds in decorSnakes)
        {
            int n = ds.segs.Count;
            if (n < 2) continue;
            int a = (int)(ds.alpha * 255);
            // 连线
            for (int i = n - 1; i >= 1; i--)
            {
                Color col = ds.pal[i % ds.pal.Length];
                using var pen = new Pen(Color.FromArgb(a, col), 11f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(pen, ds.segs[i], ds.segs[i - 1]);
            }
            // 关节
            for (int i = n - 1; i >= 0; i--)
            {
                PointF p = ds.segs[i];
                Color col = ds.pal[i % ds.pal.Length];
                g.FillEllipse(Brush(Color.FromArgb(a, col)), p.X - 6.5f, p.Y - 6.5f, 13f, 13f);
            }
        }
    }

    void DrawMenu(Graphics g, RectangleF play)
    {
        // 背景装饰小蛇
        DrawDecorSnakes(g);

        // 半透明遮罩
        FillRound(g, play, 12f, Brush(Color.FromArgb(200, 5, 7, 10)));

        // 动态背景光晕
        for (int i = 0; i < 3; i++)
        {
            float bx = play.X + play.Width * (0.2f + 0.3f * i) + MathF.Sin(gameT * 0.0004f + i * 2f) * 40;
            float by = play.Y + play.Height * (0.3f + 0.2f * i) + MathF.Cos(gameT * 0.0003f + i * 1.5f) * 30;
            Color gc = i == 0 ? Color.FromArgb(80, 230, 255) : i == 1 ? Color.FromArgb(60, 255, 100, 200) : Color.FromArgb(50, 255, 200, 60);
            g.FillEllipse(Brush(gc), bx - 80, by - 80, 160, 160);
        }

        // 扫描线
        for (float ly = play.Y; ly < play.Bottom; ly += 6)
        {
            g.FillRectangle(Brush(Color.FromArgb(8, 255, 255, 255)), play.X + 4, ly, play.Width - 8, 1);
        }

        float cx = play.X + play.Width / 2;
        float y = play.Y + 22;

        // 标题：多层霓虹光晕
        string title = "贪 吃 蛇";
        SizeF ts = g.MeasureString(title, fTitle);
        float tx = cx - ts.Width / 2;
        g.DrawString(title, fTitle, Brush(Color.FromArgb(30, Neon)), tx + 4, y + 4);
        g.DrawString(title, fTitle, Brush(Color.FromArgb(60, CurrentHead)), tx + 2, y + 2);
        g.DrawString(title, fTitle, Brush(CurrentHead), tx, y);
        y += ts.Height + 4;

        // 副标题
        string sub = "SNAKE  —  霓虹版";
        SizeF ss = g.MeasureString(sub, fMid);
        g.DrawString(sub, fMid, Brush(Color.FromArgb(180, Neon)), cx - ss.Width / 2, y);
        y += ss.Height + 8;

        // 最高分
        string hs = $"最高分  {highScore}";
        SizeF hsz = g.MeasureString(hs, fMid);
        g.DrawString(hs, fMid, Brush(Color.FromArgb(255, 190, 90)), cx - hsz.Width / 2, y);
        y += hsz.Height + 14;

        string obsText = obstacleLevel switch { ObstacleLevel.Off => "关", ObstacleLevel.Few => "少", ObstacleLevel.Many => "多", _ => "?" };
        string diffText = difficulty switch { Difficulty.Easy => "简单", Difficulty.Normal => "普通", Difficulty.Hard => "困难", _ => "?" };
        string[] items =
        {
            $"模式:  {(wallMode == WallMode.Walls ? "墙壁" : "穿墙"),4}   ← →",
            $"难度:  {diffText,4}   ← →",
            $"障碍:  {obsText,4}   ← →",
            $"皮肤:  {CurrentSkin.Name,4}   ← →",
            "开始游戏",
            "退出游戏"
        };

        // 菜单项（选中时脉冲+微移）
        for (int i = 0; i < items.Length; i++)
        {
            bool sel = i == menuIndex;
            var f = sel ? fMenuSel : fMenu;
            float pulse = sel ? 0.5f + 0.5f * MathF.Sin(gameT * 0.006f) : 0f;
            var col = sel ? Color.FromArgb(255, (int)(120 + 80 * pulse), (int)(230 + 25 * pulse)) : Color.FromArgb(160, 180, 200);
            float offX = sel ? MathF.Sin(gameT * 0.005f) * 3f : 0f;
            SizeF isz = g.MeasureString(items[i], f);
            if (sel)
            {
                var bar = new RectangleF(cx - isz.Width / 2 - 22 + offX, y - 3, isz.Width + 44, isz.Height + 6);
                FillRound(g, bar, 6f, Brush(Color.FromArgb((int)(28 + 15 * pulse), Neon)));
                using var spen = new Pen(Color.FromArgb((int)(100 + 80 * pulse), Neon), 1f);
                DrawRound(g, bar, 6f, spen);
            }
            g.DrawString(items[i], f, Brush(col), cx - isz.Width / 2 + offX, y);
            y += isz.Height + 6;
        }

        y += 10;

        // 道具栏标题
        string pt = "— 道具说明 —";
        SizeF ptsz = g.MeasureString(pt, fMid);
        g.DrawString(pt, fMid, Brush(Color.FromArgb(140, 160, 180)), cx - ptsz.Width / 2, y);
        y += ptsz.Height + 6;

        // 道具栏：5 个道具横向排列
        var powerUps = new (string name, string desc, int type)[]
        {
            ("奖励分", "+5 分", 0),
            ("加速", "提速 5 秒", 1),
            ("无敌星", "撞碎障碍", 2),
            ("减速", "减速 5 秒", 3),
            ("双倍分", "得分 x2", 4),
        };
        float itemW = play.Width / 5f;
        float iconY = y + 12;
        for (int i = 0; i < powerUps.Length; i++)
        {
            float icx = play.X + itemW * (i + 0.5f);
            DrawPowerUpIcon(g, powerUps[i].type, new PointF(icx, iconY), 10f);
            SizeF ns = g.MeasureString(powerUps[i].name, fMid);
            g.DrawString(powerUps[i].name, fMid, Brush(Color.FromArgb(210, 220, 230)), icx - ns.Width / 2, iconY + 16);
            SizeF ds = g.MeasureString(powerUps[i].desc, fHint);
            g.DrawString(powerUps[i].desc, fHint, Brush(Color.FromArgb(110, 130, 150)), icx - ds.Width / 2, iconY + 16 + ns.Height + 1);
        }

        // 底部提示
        string foot = "↑↓ 选择   ← → 调整   Enter 确认   F11 全屏";
        SizeF fsz = g.MeasureString(foot, fHint);
        g.DrawString(foot, fHint, Brush(Color.FromArgb(100, 120, 140)), cx - fsz.Width / 2, play.Bottom - fsz.Height - 12);
    }

    // ── 覆盖层 ────────────────────────────────────────────────
    void DrawDeadOverlay(Graphics g, RectangleF play)
    {
        Dim(g, play);
        CenterText(g, play, "游戏结束", fBig, Color.FromArgb(255, 110, 110), -30);
        CenterText(g, play, $"得分 {score}   最高 {highScore}", fMid, Color.FromArgb(255, 214, 80), -2);
        CenterText(g, play, "R 重开   Esc 菜单", fHint, Color.FromArgb(160, 180, 200), 26);
    }

    void DrawWonOverlay(Graphics g, RectangleF play)
    {
        Dim(g, play);
        CenterText(g, play, "通关! 你填满了整片场地", fBig, Color.FromArgb(110, 255, 160), -30);
        CenterText(g, play, $"得分 {score}   最高 {highScore}", fMid, Color.FromArgb(255, 214, 80), -2);
        CenterText(g, play, "R 重开   Esc 菜单", fHint, Color.FromArgb(160, 180, 200), 26);
    }

    void DrawPausedOverlay(Graphics g, RectangleF play)
    {
        Dim(g, play);

        float cx = play.X + play.Width / 2;
        float y = play.Y + play.Height / 2 - 70;

        // 标题
        string title = "已暂停";
        SizeF ts = g.MeasureString(title, fBig);
        g.DrawString(title, fBig, Brush(Color.FromArgb(50, Neon)), cx - ts.Width / 2 + 2, y + 2);
        g.DrawString(title, fBig, Brush(Color.FromArgb(160, 230, 255)), cx - ts.Width / 2, y);
        y += ts.Height + 24;

        string[] items = { "继续游戏", "重新开始", "返回菜单" };
        for (int i = 0; i < items.Length; i++)
        {
            bool sel = i == pauseIndex;
            var f = sel ? fMenuSel : fMenu;
            float pulse = sel ? 0.5f + 0.5f * MathF.Sin(gameT * 0.006f) : 0f;
            var col = sel ? Color.FromArgb(255, (int)(120 + 80 * pulse), (int)(230 + 25 * pulse)) : Color.FromArgb(160, 180, 200);
            SizeF isz = g.MeasureString(items[i], f);
            if (sel)
            {
                var bar = new RectangleF(cx - isz.Width / 2 - 20, y - 3, isz.Width + 40, isz.Height + 6);
                FillRound(g, bar, 6f, Brush(Color.FromArgb((int)(28 + 15 * pulse), Neon)));
                using var spen = new Pen(Color.FromArgb((int)(100 + 80 * pulse), Neon), 1f);
                DrawRound(g, bar, 6f, spen);
            }
            g.DrawString(items[i], f, Brush(col), cx - isz.Width / 2, y);
            y += isz.Height + 10;
        }

        y += 8;
        string hint = "↑↓ 选择   Enter 确认   空格 继续";
        SizeF hs = g.MeasureString(hint, fHint);
        g.DrawString(hint, fHint, Brush(Color.FromArgb(100, 120, 140)), cx - hs.Width / 2, y);
    }

    void Dim(Graphics g, RectangleF r) =>
        FillRound(g, r, 12f, Brush(Color.FromArgb(160, 5, 7, 10)));

    void CenterText(Graphics g, RectangleF area, string s, Font f, Color col, float dy)
    {
        SizeF sz = g.MeasureString(s, f);
        g.DrawString(s, f, Brush(col),
            area.X + (area.Width - sz.Width) / 2,
            area.Y + area.Height / 2 - sz.Height / 2 + dy);
    }

    // ── 圆角工具 ──────────────────────────────────────────────
    static void FillRound(Graphics g, RectangleF r, float rad, Brush b)
    {
        using GraphicsPath p = RoundPath(r, rad);
        g.FillPath(b, p);
    }

    static void DrawRound(Graphics g, RectangleF r, float rad, Pen pen)
    {
        using GraphicsPath p = RoundPath(r, rad);
        g.DrawPath(pen, p);
    }

    static GraphicsPath RoundPath(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // ── 粒子类 ────────────────────────────────────────────────
    class Particle
    {
        public float X, Y, Vx, Vy, R, Age, Life;
        public Color C;

        public void Step(int dt)
        {
            float s = dt / 1000f;
            X += Vx * s;
            Y += Vy * s;
            Age += dt;
        }
    }

    class AmbientParticle
    {
        public float X, Y, Vx, Vy, R, Alpha;

        public void Step(int dt, float w, float h)
        {
            float s = dt / 1000f;
            X += Vx * s;
            Y += Vy * s;
            if (X < 0) X += w;
            if (X > w) X -= w;
            if (Y < 0) Y += h;
            if (Y > h) Y -= h;
        }
    }

    class DecorSnake
    {
        public List<PointF> segs = new();
        public float dx, dy, speed, alpha, segGap;
        public int length;
        public Color[] pal = null!;

        public void Step(float dt, float w, float h)
        {
            if (segs.Count == 0) return;
            float s = dt / 1000f;
            // 头部移动
            segs[0] = new PointF(segs[0].X + dx * speed * s, segs[0].Y + dy * speed * s);
            // 身体跟随
            for (int i = 1; i < segs.Count; i++)
            {
                PointF prev = segs[i - 1];
                PointF cur = segs[i];
                float ddx = prev.X - cur.X;
                float ddy = prev.Y - cur.Y;
                float dist = MathF.Sqrt(ddx * ddx + ddy * ddy);
                if (dist > segGap)
                {
                    float f = (dist - segGap) / dist;
                    segs[i] = new PointF(cur.X + ddx * f, cur.Y + ddy * f);
                }
            }
            // 循环边界
            if (segs[0].X > w + 120) segs[0] = new PointF(-120, segs[0].Y);
            if (segs[0].X < -120) segs[0] = new PointF(w + 120, segs[0].Y);
            if (segs[0].Y > h + 60) segs[0] = new PointF(segs[0].X, -60);
            if (segs[0].Y < -60) segs[0] = new PointF(segs[0].X, h + 60);
        }
    }

    class FloatingText
    {
        public string Text = "";
        public float X, Y, Vy, Alpha, Size;
        public int Age, Life;
        public Color Col;
    }

    class Shockwave
    {
        public float X, Y, R, MaxR, Alpha, Width;
        public Color Col;
        public int Age, Life;
    }

    // ── 入口 ──────────────────────────────────────────────────
    [STAThread]
    static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => LogCrash(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) LogCrash(ex);
        };
        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new SnakeForm());
        }
        catch (Exception ex)
        {
            LogCrash(ex);
        }
    }

    static void LogCrash(Exception ex)
    {
        string log = Path.Combine(AppContext.BaseDirectory, "crash.log");
        File.WriteAllText(log, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{ex}\n{ex.StackTrace}");
        MessageBox.Show($"出错了，详情已写入:\n{log}\n\n{ex.Message}", "贪吃蛇",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

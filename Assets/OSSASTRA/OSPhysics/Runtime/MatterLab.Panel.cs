// 职责：绘制并命中实验室面板。画笔、视图比较、临时颜色值都在运行态，探针只读。
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ossastra.OSPhysics
{
    public enum MatterLabControl
    {
        Water, Oil, Sand, Grass, Wood, Heat, Erase, Barrier,
        Radius, Strength, Run, Step, Reset, SpeedHalf, SpeedNormal, SpeedFast,
        Gate, Clear, Frame, Flow, Layering, Combustion, Empty,
        Natural, Grid, Temperature, Collision, Stats,
        Painted, WorldLight, Ruins, Moss, PrivateArt, Background, Body, Trim, Decor, Foreground,
        PreviewDark, PreviewNormal, PreviewBright, LightLeft, LightRight, WaterOpacity, EffectGain, ResetLook
    }

    public sealed partial class MatterLab
    {
        private static readonly Color Panel = new Color(.045f, .078f, .094f, .97f);
        private static readonly Color BlueGreen = new Color(.36f, .83f, .76f);
        private static readonly Color Text = new Color(.89f, .95f, .95f);
        private static readonly Color Muted = new Color(.65f, .75f, .76f);
        private static readonly Color Border = new Color(.18f, .31f, .34f);
        private static readonly Color ButtonBase = new Color(.095f, .15f, .18f);
        private static readonly Color ButtonHot = new Color(.14f, .23f, .25f);
        private static readonly Color ButtonActive = new Color(.14f, .33f, .33f);
        private static readonly Color Amber = new Color(1f, .76f, .44f);
        private static readonly Color[] BrushColors =
        {
            new Color(.42f,.78f,1f), new Color(.93f,.73f,.40f), new Color(.96f,.84f,.61f),
            new Color(.64f,.87f,.52f), new Color(.81f,.63f,.49f), new Color(1f,.64f,.38f),
            new Color(.81f,.84f,.85f), new Color(.69f,.75f,.84f)
        };
        private static readonly string[] BrushNames = { "水", "油", "沙", "草", "木材", "加热", "擦除", "障碍" };
        private static readonly MatterLabControl[] Controls = (MatterLabControl[])Enum.GetValues(typeof(MatterLabControl));

        private GUIStyle labelStyle;
        private GUIStyle keyStyle;
        private GUIStyle regionStyle;
        private Font panelFont;
        private MatterLabControl? activeSlider;
        private bool showStats = true;
        private string probe;
        private string metricsLine;
        private string waterLine;
        private string oilLine;
        private float nextUiRefresh;

        private float PanelScale => Mathf.Clamp(Mathf.Min((Screen.height - 24) / 885f,
            (Screen.width - 24) / 1020f), .45f, 1.25f);
        public Rect PanelRect => new Rect(Screen.width - 14 - 334 * PanelScale, 12,
            334 * PanelScale, 885 * PanelScale);

        private Rect Item(float x, float y, float width, float height)
        {
            Rect panel = PanelRect;
            float scale = PanelScale;
            return new Rect(panel.x + x * scale, panel.y + y * scale, width * scale, height * scale);
        }

        public Rect ControlRect(MatterLabControl control)
        {
            int code = (int)control;
            if (code <= (int)MatterLabControl.Barrier) return Item(14 + code % 4 * 77, 95 + code / 4 * 37, 74, 33);
            return control switch
            {
                MatterLabControl.Radius => Item(114, 183, 162, 21),
                MatterLabControl.Strength => Item(114, 213, 162, 21),
                MatterLabControl.Run => Item(14, 262, 100, 31),
                MatterLabControl.Step => Item(117, 262, 100, 31),
                MatterLabControl.Reset => Item(220, 262, 100, 31),
                MatterLabControl.SpeedHalf => Item(14, 297, 100, 27),
                MatterLabControl.SpeedNormal => Item(117, 297, 100, 27),
                MatterLabControl.SpeedFast => Item(220, 297, 100, 27),
                MatterLabControl.Gate => Item(14, 335, 100, 31),
                MatterLabControl.Clear => Item(117, 335, 100, 31),
                MatterLabControl.Frame => Item(220, 335, 100, 31),
                MatterLabControl.Flow => Item(14, 403, 74, 31),
                MatterLabControl.Layering => Item(91, 403, 74, 31),
                MatterLabControl.Combustion => Item(168, 403, 74, 31),
                MatterLabControl.Empty => Item(245, 403, 74, 31),
                MatterLabControl.Grid => Item(14, 473, 74, 31),
                MatterLabControl.Natural => Item(91, 473, 74, 31),
                MatterLabControl.Temperature => Item(14, 510, 74, 28),
                MatterLabControl.Collision => Item(91, 510, 74, 28),
                MatterLabControl.Stats => Item(14, 792, 306, 23),
                MatterLabControl.Painted => Item(168, 473, 74, 31),
                MatterLabControl.WorldLight => Item(245, 473, 74, 31),
                MatterLabControl.Ruins => Item(168, 510, 74, 28),
                MatterLabControl.Moss => Item(245, 510, 74, 28),
                MatterLabControl.PrivateArt => Item(14, 544, 306, 28),
                MatterLabControl.Background => Item(14, 578, 59, 26),
                MatterLabControl.Body => Item(76, 578, 59, 26),
                MatterLabControl.Trim => Item(138, 578, 59, 26),
                MatterLabControl.Decor => Item(200, 578, 59, 26),
                MatterLabControl.Foreground => Item(262, 578, 58, 26),
                MatterLabControl.PreviewDark => Item(14, 610, 59, 26),
                MatterLabControl.PreviewNormal => Item(76, 610, 59, 26),
                MatterLabControl.PreviewBright => Item(138, 610, 59, 26),
                MatterLabControl.LightLeft => Item(200, 610, 59, 26),
                MatterLabControl.LightRight => Item(262, 610, 58, 26),
                MatterLabControl.WaterOpacity => Item(114, 669, 162, 21),
                MatterLabControl.EffectGain => Item(114, 697, 162, 21),
                MatterLabControl.ResetLook => Item(14, 724, 306, 24),
                _ => default
            };
        }

        private void HandlePanelClick(Vector2 point)
        {
            foreach (var control in Controls)
            {
                if (!ControlRect(control).Contains(point)) continue;
                if (control <= MatterLabControl.Barrier)
                {
                    brush = (MatterBrush)control;
                    return;
                }
                switch (control)
                {
                    case MatterLabControl.Radius:
                    case MatterLabControl.Strength:
                    case MatterLabControl.WaterOpacity:
                    case MatterLabControl.EffectGain:
                        activeSlider = control;
                        UpdateSlider(point);
                        break;
                    case MatterLabControl.Run: Running = !Running; break;
                    case MatterLabControl.Step: StepOnce(); break;
                    case MatterLabControl.Reset: ResetLab(); break;
                    case MatterLabControl.SpeedHalf: speed = .5f; break;
                    case MatterLabControl.SpeedNormal: speed = 1f; break;
                    case MatterLabControl.SpeedFast: speed = 2f; break;
                    case MatterLabControl.Gate: ToggleGate(); break;
                    case MatterLabControl.Clear: ClearDynamic(); break;
                    case MatterLabControl.Frame: FrameWorld(); break;
                    case MatterLabControl.Flow: SelectPreset(MatterLabPreset.Flow); break;
                    case MatterLabControl.Layering: SelectPreset(MatterLabPreset.Layering); break;
                    case MatterLabControl.Combustion: SelectPreset(MatterLabPreset.Combustion); break;
                    case MatterLabControl.Empty: SelectPreset(MatterLabPreset.Empty); break;
                    case MatterLabControl.Natural: SetDisplay(MatterDisplay.Natural); break;
                    case MatterLabControl.Grid: SetDisplay(MatterDisplay.Grid); break;
                    case MatterLabControl.Temperature: SetDisplay(MatterDisplay.Heat); break;
                    case MatterLabControl.Collision: SetDisplay(MatterDisplay.Collision); break;
                    case MatterLabControl.Stats: showStats = !showStats; break;
                    case MatterLabControl.Painted: SetDisplay(MatterDisplay.Painted); break;
                    case MatterLabControl.WorldLight: SetDisplay(MatterDisplay.PaintedWorldLight); break;
                    case MatterLabControl.Ruins: SetPaintStyle(MatterPaintStyle.Ruins); break;
                    case MatterLabControl.Moss: SetPaintStyle(MatterPaintStyle.Moss); break;
                    case MatterLabControl.PrivateArt: SetPrivateArt(!privateArt); break;
                    case MatterLabControl.Background: TogglePaintLayer(MatterPaintLayers.Background); break;
                    case MatterLabControl.Body: TogglePaintLayer(MatterPaintLayers.Body); break;
                    case MatterLabControl.Trim: TogglePaintLayer(MatterPaintLayers.Trim); break;
                    case MatterLabControl.Decor: TogglePaintLayer(MatterPaintLayers.Decoration); break;
                    case MatterLabControl.Foreground: TogglePaintLayer(MatterPaintLayers.Foreground); break;
                    case MatterLabControl.PreviewDark: SetPaintMood(.16f); break;
                    case MatterLabControl.PreviewNormal: SetPaintMood(1f); break;
                    case MatterLabControl.PreviewBright: SetPaintMood(1.72f); break;
                    case MatterLabControl.LightLeft: SetPreviewLamp(-5.1f); break;
                    case MatterLabControl.LightRight: SetPreviewLamp(5.1f); break;
                    case MatterLabControl.ResetLook:
                        paintLayers = MatterPaintLayers.All;
                        paintMood = waterOpacity = effectGain = 1;
                        lampX = 0;
                        break;
                }
                return;
            }
        }

        private void UpdateSlider(Vector2 point)
        {
            if (!activeSlider.HasValue) return;
            Rect rect = ControlRect(activeSlider.Value);
            float percent = Mathf.InverseLerp(rect.xMin, rect.xMax, point.x);
            switch (activeSlider)
            {
                case MatterLabControl.Radius: brushRadius = Mathf.RoundToInt(Mathf.Lerp(1, 16, percent)); break;
                case MatterLabControl.Strength: brushStrength = Mathf.RoundToInt(Mathf.Lerp(24, 255, percent)); break;
                case MatterLabControl.WaterOpacity: waterOpacity = Mathf.Lerp(.42f, 1.25f, percent); break;
                case MatterLabControl.EffectGain: effectGain = Mathf.Lerp(0, 1.6f, percent); break;
            }
        }

        private void OnGUI()
        {
            if (!Ready || Event.current.type != EventType.Repaint) return;
            EnsureStyles();
            var mouse = Mouse.current;
            Vector2 pointer = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            Vector2 gui = new Vector2(pointer.x, Screen.height - pointer.y);

            DrawRegions(gui, pointer);
            Box(PanelRect, Panel);
            Box(Item(0, 0, 334, 3), BlueGreen);
            Write(Item(14, 12, 306, 30), "动态物质实验室", 24, Text, true);
            Write(Item(14, 46, 308, 20), "左键绘制  ·  中键 / WASD 平移  ·  滚轮缩放", 12, Muted);
            Section(73, "画笔");
            for (int i = 0; i < BrushNames.Length; i++)
                Button((MatterLabControl)i, BrushNames[i], gui, (int)brush == i, true, BrushColors[i], (i + 1).ToString());
            Slider(MatterLabControl.Radius, "半径（格）", brushRadius.ToString(), (brushRadius - 1) / 15f, 183);
            Slider(MatterLabControl.Strength, brush == MatterBrush.Heat ? "热量 / 步" : "施加量 / 步",
                Strength(brush).ToString(), (brushStrength - 24) / 231f, 213);

            Section(243, "模拟");
            Button(MatterLabControl.Run, Running ? "暂停" : "继续", gui, Running, true);
            Button(MatterLabControl.Step, "单步", gui, false, !Running);
            Button(MatterLabControl.Reset, "重置布置", gui);
            Button(MatterLabControl.SpeedHalf, "0.5×", gui, speed == .5f);
            Button(MatterLabControl.SpeedNormal, "1×", gui, speed == 1f);
            Button(MatterLabControl.SpeedFast, "2×", gui, speed == 2f);
            Button(MatterLabControl.Gate, GateOpen ? "关闭闸门" : "打开闸门", gui, GateOpen);
            Button(MatterLabControl.Clear, "清空物质", gui);
            Button(MatterLabControl.Frame, "镜头复位", gui);
            Section(379, "初始布置");
            Button(MatterLabControl.Flow, "蓄水泄流", gui, currentPreset == MatterLabPreset.Flow);
            Button(MatterLabControl.Layering, "油水分层", gui, currentPreset == MatterLabPreset.Layering);
            Button(MatterLabControl.Combustion, "燃烧灭火", gui, currentPreset == MatterLabPreset.Combustion);
            Button(MatterLabControl.Empty, "自由创作", gui, currentPreset == MatterLabPreset.Empty);

            Section(448, "画面");
            bool handPainted = display == MatterDisplay.Painted || display == MatterDisplay.PaintedWorldLight;
            Button(MatterLabControl.Grid, "A · 网格", gui, display == MatterDisplay.Grid);
            Button(MatterLabControl.Natural, "B · 程序", gui, display == MatterDisplay.Natural);
            Button(MatterLabControl.Painted, "C · 手绘", gui, display == MatterDisplay.Painted);
            Button(MatterLabControl.WorldLight, "世界灯图", gui, display == MatterDisplay.PaintedWorldLight);
            Button(MatterLabControl.Temperature, "温度", gui, display == MatterDisplay.Heat);
            Button(MatterLabControl.Collision, "碰撞", gui, display == MatterDisplay.Collision);
            Button(MatterLabControl.Ruins, "雾蓝水渠", gui, paintStyle == MatterPaintStyle.Ruins);
            Button(MatterLabControl.Moss, "苔光幽洞", gui, paintStyle == MatterPaintStyle.Moss);
            Button(MatterLabControl.PrivateArt, privateArt ? "还原原创配置" : "载入本机素材映射", gui, privateArt, handPainted);
            Button(MatterLabControl.Background, "背景", gui, HasPaintLayer(MatterPaintLayers.Background), handPainted);
            Button(MatterLabControl.Body, "石体", gui, HasPaintLayer(MatterPaintLayers.Body), handPainted);
            Button(MatterLabControl.Trim, "饰边", gui, HasPaintLayer(MatterPaintLayers.Trim), handPainted);
            Button(MatterLabControl.Decor, "装饰", gui, HasPaintLayer(MatterPaintLayers.Decoration), handPainted);
            Button(MatterLabControl.Foreground, "前景", gui, HasPaintLayer(MatterPaintLayers.Foreground), handPainted);
            Button(MatterLabControl.PreviewDark, "偏暗", gui, paintMood < .5f, handPainted);
            Button(MatterLabControl.PreviewNormal, "正常", gui, paintMood == 1, handPainted);
            Button(MatterLabControl.PreviewBright, "偏亮", gui, paintMood > 1, handPainted);
            bool mapSelected = display == MatterDisplay.PaintedWorldLight;
            Button(MatterLabControl.LightLeft, "左灯", gui, lampX < 0, mapSelected);
            Button(MatterLabControl.LightRight, "右灯", gui, lampX > 0, mapSelected);
            string lookStatus = mapSelected ? visual.PaintedLightStatus : (handPainted ? visual.PaintedArtStatus : "诊断与旧程序视图");
            Write(Item(14, 641, 306, 23), lookStatus, 12, mapSelected && !visual.WorldLightLive ? Amber : Muted);
            Slider(MatterLabControl.WaterOpacity, "水体透明度", $"{waterOpacity:0.00}×", Mathf.InverseLerp(.42f, 1.25f, waterOpacity), 669);
            Slider(MatterLabControl.EffectGain, "火烟强度", $"{effectGain:0.00}×", effectGain / 1.6f, 697);
            Button(MatterLabControl.ResetLook, "恢复画面推荐值", gui);

            RefreshLabels(gui, pointer);
            string message = Time.unscaledTime < feedbackUntil && feedback != null ? feedback : probe;
            Write(Item(14, 752, 306, 35), message, 12, feedback != null && Time.unscaledTime < feedbackUntil ? Amber : Muted);
            Button(MatterLabControl.Stats, showStats ? "▾ 步时与物质账本" : "▸ 步时与物质账本", gui);
            if (!showStats) return;
            Write(Item(14, 817, 306, 29), metricsLine, 11, Text);
            Write(Item(14, 848, 306, 15), waterLine, 11, BrushColors[0]);
            Write(Item(14, 864, 306, 15), oilLine, 11, BrushColors[1]);
        }

        private bool HasPaintLayer(MatterPaintLayers layer) => (paintLayers & layer) != 0;

        private void EnsureStyles()
        {
            if (labelStyle != null) return;
            panelFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei" }, 16);
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = panelFont != null ? panelFont : GUI.skin.font,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            keyStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.UpperLeft };
            regionStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter };
        }

        private void OnDestroy()
        {
            if (panelFont != null) Destroy(panelFont);
        }

        private void RefreshLabels(Vector2 gui, Vector2 pointer)
        {
            if (Time.unscaledTime < nextUiRefresh) return;
            nextUiRefresh = Time.unscaledTime + .12f;
            probe = "将鼠标移到实验区查看格内物质与热量。";
            if (!PanelRect.Contains(gui) && TryScreenPoint(pointer, out Vector2 point) &&
                WorldToCell(point, out int x, out int y) && World.TrySnapshot(x, y, out var sample))
            {
                string kind = sample.Fixed ? "固定地形" : sample.Barrier ? "阻挡" : NameOf(sample.Kind);
                probe = $"格 ({x}, {y})  {kind}  数量 {sample.Amount}\n热量 {sample.Heat}  燃料 {sample.Fuel}  烟 {sample.Smoke}" +
                    (sample.Burning ? "  燃烧中" : "");
            }
            var metrics = World.Metrics;
            string hz = Running ? $"{actualStepRate:0}/{MatterLabClock.Frequency * speed:0} Hz" : "暂停";
            metricsLine = $"{fps:0} FPS    {metrics.StepMilliseconds:0.00} ms/步    {hz}\n";
            metricsLine += $"活跃 {metrics.ActiveChunks} 块 · 上传 {visual.LastUploadedChunks} · 绘制 {visual.PaintedBatchCount} · 灯图 {visual.PaintedLightMaps}";
            waterLine = Ledger("水", Matter.Water);
            oilLine = Ledger("油", Matter.Oil);
        }

        private string Ledger(string label, Matter kind)
        {
            var ledger = World.GetLedger(kind);
            long delta = ledger.Initial + ledger.Injected - ledger.Removed - ledger.Reacted - ledger.Lost - ledger.Current;
            return $"{label}  {ledger.Current:N0}   入 {ledger.Injected:N0}   擦 {ledger.Removed:N0}   耗 {ledger.Reacted:N0}   差 {delta}";
        }

        private static string NameOf(Matter kind) => kind == Matter.Empty ? "空" :
            kind == Matter.Ash ? "灰" : BrushNames[(int)kind - (int)Matter.Water];

        private void Section(float y, string title)
        {
            Write(Item(14, y, 66, 18), title, 13, BlueGreen, true);
            Box(Item(86, y + 9, 233, 1), Border);
        }

        private void Slider(MatterLabControl control, string text, string value, float fraction, float top)
        {
            Rect track = ControlRect(control);
            float scale = PanelScale;
            Box(new Rect(track.x, track.center.y - 2 * scale, track.width, 4 * scale), Border);
            Box(new Rect(track.x, track.center.y - 2 * scale, track.width * fraction, 4 * scale), BlueGreen);
            Box(new Rect(track.x + track.width * fraction - 4 * scale, track.center.y - 5 * scale,
                8 * scale, 10 * scale), Text);
            Write(Item(14, top, 99, 21), text, 12, Muted);
            Write(Item(287, top, 32, 21), value, 12, Text);
        }

        private void Button(MatterLabControl control, string title, Vector2 pointer, bool selected = false,
            bool enabled = true, Color? accent = null, string key = null)
        {
            Rect rect = ControlRect(control);
            Color background = selected ? ButtonActive : enabled && rect.Contains(pointer) ? ButtonHot : ButtonBase;
            Box(rect, background);
            if (selected || accent.HasValue)
                Box(new Rect(rect.x, rect.y, rect.width, PanelScale), selected ? BlueGreen : accent.Value);
            Write(rect, title, 13, enabled ? accent ?? Text : Muted, selected, true);
            if (key != null)
            {
                keyStyle.fontSize = Mathf.RoundToInt(10 * PanelScale);
                keyStyle.normal.textColor = Muted;
                var tag = new Rect(rect.x + 2 * PanelScale, rect.y, rect.width, rect.height);
                GUI.Label(tag, key, keyStyle);
            }
        }

        private void Write(Rect rect, string content, float size, Color color, bool bold = false, bool center = false)
        {
            labelStyle.fontSize = Mathf.RoundToInt(size * PanelScale);
            labelStyle.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            labelStyle.normal.textColor = color;
            labelStyle.alignment = center ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            GUI.Label(rect, content, labelStyle);
        }

        private void DrawRegions(Vector2 gui, Vector2 pointer)
        {
            DrawRegion(29, 111, "自由创作");
            DrawRegion(104, 111, "重力与闸门");
            DrawRegion(180, 111, "燃烧与灭火");
            DrawRegion(230, 111, "明暗试验");
            if (PanelRect.Contains(gui) || !TryScreenPoint(pointer, out Vector2 point) ||
                !WorldToCell(point, out int x, out int y)) return;
            Vector3 pixel = labCamera.WorldToScreenPoint(CellToWorld(x, y));
            float radius = (brushRadius - .5f) * CellSize * Screen.height / (2 * labCamera.orthographicSize);
            Vector2 center = new Vector2(pixel.x, Screen.height - pixel.y);
            Color color = BrushColors[(int)brush];
            color.a = .73f;
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI / 12;
                Vector2 dot = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (!PanelRect.Contains(dot))
                    Box(new Rect(dot.x - 1, dot.y - 1, 2, 2), color);
            }
        }

        private void DrawRegion(int x, int y, string title)
        {
            Vector3 pixel = labCamera.WorldToScreenPoint(CellToWorld(x, y));
            float width = 111 * PanelScale;
            var rect = new Rect(pixel.x - width * .5f, Screen.height - pixel.y - 13 * PanelScale, width,
                24 * PanelScale + 1);
            if (pixel.z < 0 || rect.y < 20 || rect.yMax >= Screen.height || rect.xMin < 0 ||
                rect.xMax > PanelRect.xMin - 6) return;
            regionStyle.fontSize = Mathf.RoundToInt(13 * PanelScale);
            regionStyle.normal.textColor = Text;
            Box(rect, new Color(.045f, .078f, .094f, .76f));
            GUI.Label(rect, title, regionStyle);
        }

        private static void Box(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}

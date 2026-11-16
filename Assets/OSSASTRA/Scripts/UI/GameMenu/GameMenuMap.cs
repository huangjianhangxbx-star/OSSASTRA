// 显示样例地图并提供会话标记和浏览。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using static Ossastra.UI.GameMenu.MenuPageBuilder;

namespace Ossastra.UI.GameMenu
{
    internal sealed class GameMenuMap
    {
        private readonly struct Room
        {
            public readonly float X, Y, W, H;
            public readonly bool Visited;
            public readonly int Previous;
            public Room(float x, float y, float width, float height, bool visited, int previous)
            {
                X = x;
                Y = y;
                W = width;
                H = height;
                Visited = visited;
                Previous = previous;
            }
        }

        private static readonly string[] RegionNames = { "回声塔", "旧城区", "雾下矿道" };
        private static readonly string[] RestNames = { "入口记录点", "旧桥休息点", "矿道哨站" };
        private static readonly string[] Legend = { "玩家", "休息点", "开关", "门", "锁定门", "隐蔽通道",
            "尚未开放", "商店", "物品", "区域移动", "未探索地带", "已探索地带" };
        private static readonly Color[] LegendColors =
        {
            new Color32(100,220,247,255), new Color32(104,207,235,255), new Color32(220,244,242,255),
            new Color32(203,240,241,255), new Color32(227,188,102,255), new Color32(121,211,216,255),
            new Color32(206,105,107,255), new Color32(183,159,241,255), new Color32(192,153,237,255),
            new Color32(144,244,104,255), new Color32(111,116,120,255), new Color32(55,153,209,255)
        };

        private static readonly Room[] Tower =
        {
            new Room(35,51,115,43,false,-1), new Room(149,64,43,21,true,0),
            new Room(192,57,91,30,true,1), new Room(282,58,47,35,true,2),
            new Room(328,49,146,44,true,3), new Room(474,57,92,39,false,4),
            new Room(90,190,117,38,true,1), new Room(207,184,111,44,true,6),
            new Room(317,191,129,33,true,7), new Room(435,173,111,55,true,8),
            new Room(544,183,172,51,false,9), new Room(715,217,83,40,true,10),
            new Room(797,236,102,32,true,11), new Room(882,261,93,55,true,12),
            new Room(915,316,42,56,true,13), new Room(922,370,81,48,true,14),
            new Room(994,415,112,66,true,15), new Room(974,479,118,81,false,16),
            new Room(1064,542,57,83,false,17), new Room(808,454,105,52,false,15),
            new Room(737,394,71,53,false,19), new Room(668,365,74,55,false,20)
        };

        private static readonly Room[] Town =
        {
            new Room(68,339,153,94,true,-1), new Room(220,370,151,29,true,0),
            new Room(367,346,114,72,true,1), new Room(430,264,51,85,true,2),
            new Room(479,265,158,50,true,3), new Room(635,247,142,71,true,4),
            new Room(765,253,104,64,false,5), new Room(867,238,105,76,false,6),
            new Room(432,175,53,91,true,3), new Room(473,122,114,61,true,8),
            new Room(586,147,108,31,false,9), new Room(354,134,116,56,true,9),
            new Room(468,417,155,55,true,2), new Room(620,431,147,58,true,12),
            new Room(758,422,76,72,false,13), new Room(830,441,114,45,false,14),
            new Room(602,500,55,86,false,13), new Room(651,565,126,37,false,16)
        };

        private static readonly Room[] Mine =
        {
            new Room(85,89,128,55,true,-1), new Room(211,98,160,35,true,0),
            new Room(369,77,117,66,true,1), new Room(398,141,47,111,true,2),
            new Room(430,225,126,56,true,3), new Room(554,234,163,29,false,4),
            new Room(716,210,117,65,false,5), new Room(402,249,46,102,true,3),
            new Room(347,338,172,62,true,7), new Room(161,356,187,34,true,8),
            new Room(64,343,96,59,false,9), new Room(515,352,118,36,true,8),
            new Room(624,346,137,55,true,11), new Room(727,396,42,83,false,12),
            new Room(743,468,140,64,false,13), new Room(864,492,128,39,false,14),
            new Room(993,469,100,61,false,15), new Room(707,522,43,105,false,14)
        };

        private readonly MenuPageBuilder view;
        private readonly List<Vector2>[] marks = { new List<Vector2>(), new List<Vector2>(), new List<Vector2>() };
        private RectTransform clip, canvas, drawing, pointLayer, dialog;
        private UnityEngine.UI.Text regionText, counter;
        private Vector2 pan;
        private float zoom = 1f;
        private int region;
        private bool dragging;

        internal GameMenuMap(MenuPageBuilder view) => this.view = view;

        internal void Build()
        {
            var body = view.Body;
            var legend = Box(body, "Map legend", 84, 177, 204, 711);
            for (int index = 0; index < Legend.Length; index++)
            {
                float y = index * 53;
                var color = LegendColors[index];
                if (index == 0 || index == 1)
                {
                    Shape(legend, "Legend emblem", 0, y + 7, 23, 23, index == 0 ? 24 : 4, 2.5f, color);
                    Shape(legend, "Legend medallion", 8, y + 15, 7, 7, 12, 0, color);
                }
                else if (index < 10)
                    Diamond(legend, "Legend emblem", 12, y + 19, index < 5 ? 10 : 7, color);
                else
                    Shape(legend, "Legend swatch", 1, y + 8, 24, 23, 4, 2.2f, color, 45);
                view.Label(legend, "Legend label " + index, Legend[index], 37, y, 171, 37, 23, TextColor);
            }

            clip = Box(body, "Map interactive viewport", 285, 244, 1350, 723);
            Image(clip, new Color32(255, 255, 255, 0), true);
            clip.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var gestures = clip.gameObject.AddComponent<MenuMapPointer>();
            gestures.Drag = delta => { dragging = true; Pan(delta); };
            gestures.Begin = () => dragging = false;
            gestures.End = () => dragging = false;
            gestures.Wheel = delta => Zoom(delta, gestures.LastPoint);
            gestures.Click = screen => { if (!dragging) Place(PointInClip(screen)); };

            canvas = Box(clip, "Map content", 0, 0, 1280, 695);
            canvas.anchorMin = canvas.anchorMax = canvas.pivot = new Vector2(.5f, .5f);
            canvas.anchoredPosition = Vector2.zero;
            var position = Box(clip, "Map reticle", 0, 0, 53, 53);
            position.anchorMin = position.anchorMax = position.pivot = new Vector2(.5f, .5f);
            position.anchoredPosition = Vector2.zero;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                var x = sign == -1 ? 0 : 35;
                var y = sign == -1 ? 0 : 35;
                Fill(position, "Reticle horizontal", x, 25, 18, 2.5f, Cyan);
                Fill(position, "Reticle vertical", 25, y, 2.5f, 18, Cyan);
            }

            var top = Box(body, "Map region picker", 702, 169, 516, 58);
            var strip = Image(top, new Color32(12, 21, 26, 147));
            strip.raycastTarget = true;
            vArrow(top, 24, false);
            vArrow(top, 491, true);
            view.Keycap(top, "1", 39, 14);
            view.Keycap(top, "3", 437, 14);
            view.Click(Box(top, "Previous region", 0, 0, 119, 58), () => Change(-1));
            view.Click(Box(top, "Next region", 396, 0, 119, 58), () => Change(1));
            regionText = view.Label(top, "Region name", "", 114, 0, 290, 58, 32, TextColor, TextAnchor.MiddleCenter);

            var goal = Box(body, "Map objective", 1614, 168, 234, 64);
            Image(goal, new Color32(15, 24, 26, 178));
            view.Rule(goal, 1, 1, 231, Line, false);
            view.Rule(goal, 1, 62, 231, Line, false);
            Shape(goal, "Objective frame", 18, 13, 40, 40, 4, 2.2f, new Color32(239, 175, 102, 255));
            Shape(goal, "Objective center", 27, 22, 23, 23, 4, 1.8f, new Color32(252, 218, 168, 255));
            view.Label(goal, "Objective text", "底层的探索", 70, 1, 156, 62, 24);

            var pins = Box(body, "Map marker controls", 1552, 870, 303, 104);
            Image(pins, new Color32(12, 21, 25, 173));
            view.Rule(pins, 0, 0, 303, Line, false);
            view.Rule(pins, 0, 103, 303, Line, false);
            counter = view.Label(pins, "Marker count", "", 16, 4, 264, 44, 25);
            view.Keycap(pins, "Enter", 14, 59, 63, 18);
            view.Label(pins, "Marker hint", "设置／移除标记", 92, 55, 201, 40, 21, Muted);

            view.Keycap(body, "Tab", 764, 981, 48);
            view.Label(body, "Center hint", "返回当前位置", 825, 976, 193, 42, 23);
            view.Keycap(body, "Shift", 1073, 981, 70);
            view.Label(body, "Rest hint", "休息点列表", 1156, 976, 169, 42, 23);
            view.Label(body, "Zoom hint", "滚轮  缩放", 1343, 976, 221, 42, 23);

            MakeRestList();
            view.Nav.ChangeContext = Change;
            view.Nav.Direction = direction =>
            {
                if (dialog.gameObject.activeSelf) return false;
                Pan((Vector2)direction * -38f);
                return true;
            };
            view.Nav.ConfirmWithoutTarget = () => Place(Vector2.zero);
            view.Nav.Back = CloseRests;
            view.Nav.Extra = Extra;
            view.Nav.SessionReset = () =>
            {
                foreach (var set in marks) set.Clear();
                CloseRests();
                RedrawMarkers();
            };
            Render();
        }

        private void vArrow(RectTransform parent, float x, bool right)
        {
            Shape(parent, right ? "Next region ornament" : "Previous region ornament",
                x - 20, 8, 40, 41, 4, 2.3f, Silver);
            view.Label(parent, "Direction", right ? "›" : "‹", x - 11, 2, 22, 52, 27, Silver, TextAnchor.MiddleCenter);
        }

        private void MakeRestList()
        {
            dialog = Box(view.Body, "Map rest point list", 688, 299, 559, 430);
            var shield = Box(dialog, "Modal surface", 0, 0, 559, 430);
            Image(shield, new Color32(9, 16, 21, 250), true);
            view.BandLabel(dialog, "休息点列表", 14, 15, 530, true);
            for (int index = 0; index < RestNames.Length; index++)
            {
                var n = index;
                var entry = Box(dialog, "Rest point " + RestNames[index], 30, 75 + index * 81, 499, 68);
                Image(entry, new Color32(34, 52, 60, 185));
                Shape(entry, "Rest icon", 15, 21, 23, 23, 4, 2.2f, Cyan);
                view.Label(entry, "Rest name", RestNames[index], 61, 0, 396, 68, 26);
                var pick = view.Button(entry, null, () => SelectRest(n));
                entry.GetComponent<MenuPagePointer>().Clicked = () => { pick(); SelectRest(n); };
            }
            var back = Box(dialog, "Close rests", 392, 345, 136, 48);
            Image(back, Row);
            view.Label(back, "Rest list close", "返回", 0, 0, 136, 48, 25, TextColor, TextAnchor.MiddleCenter);
            var focus = view.Button(back, null, () => CloseRests());
            back.GetComponent<MenuPagePointer>().Clicked = () => { focus(); CloseRests(); };
            dialog.gameObject.SetActive(false);
        }

        private bool Change(int offset)
        {
            if (dialog.gameObject.activeSelf) return false;
            region = (region + offset + RegionNames.Length) % RegionNames.Length;
            zoom = 1f;
            pan = Vector2.zero;
            Render();
            return true;
        }

        private void SelectRest(int rest)
        {
            CloseRests();
            Pan(new Vector2(94 - rest * 130, 12 + rest * 85) - pan);
        }

        private bool CloseRests()
        {
            if (!dialog.gameObject.activeSelf) return false;
            dialog.gameObject.SetActive(false);
            view.Nav.RefreshFocus();
            return true;
        }

        private bool Extra(string action)
        {
            switch (action?.ToLowerInvariant())
            {
                case "map.recenter":
                case "recenter":
                case "tab":
                    if (dialog.gameObject.activeSelf) return false;
                    pan = Vector2.zero;
                    zoom = 1f;
                    ApplyView();
                    return true;
                case "map.rests":
                case "rest-list":
                case "rests":
                case "shift":
                    if (!CloseRests())
                    {
                        dialog.gameObject.SetActive(true);
                        view.Nav.RefreshFocus();
                    }
                    return true;
                case "map.zoom-in":
                case "zoom-in":
                    Zoom(1, Vector2.zero);
                    return true;
                case "map.zoom-out":
                case "zoom-out":
                    Zoom(-1, Vector2.zero);
                    return true;
                case "map.clear-marker":
                case "clear-marker":
                case "reset-marker":
                    if (dialog.gameObject.activeSelf) return false;
                    marks[region].Clear();
                    RedrawMarkers();
                    return true;
                default:
                    return false;
            }
        }

        private void Render()
        {
            if (drawing != null)
            {
                drawing.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(drawing.gameObject);
            }
            regionText.text = "▣  " + RegionNames[region];
            drawing = Box(canvas, "Region geometry", 0, 0, 1280, 695);
            var regional = Box(drawing, "Hall network", 49 + region * 23, 2, 1196, 652);
            var rooms = region == 0 ? Tower : region == 1 ? Town : Mine;
            for (int index = 0; index < rooms.Length; index++)
            {
                var room = rooms[index];
                if (room.Previous >= 0)
                {
                    var from = RoomCenter(rooms[room.Previous]);
                    var to = RoomCenter(room);
                    var corner = new Vector2(from.x, to.y);
                    var lineColor = new Color32(100, 137, 154, 155);
                    Segment(regional, from, corner, lineColor);
                    Segment(regional, corner, to, lineColor);
                }
            }
            for (int index = 0; index < rooms.Length; index++) DrawRoom(regional, rooms[index], index);
            var restOne = RoomCenter(rooms[3]);
            var restTwo = RoomCenter(rooms[11]);
            var player = RoomCenter(rooms[region == 0 ? 15 : 8]);
            MapIcon(regional, restOne.x, restOne.y, LegendColors[1], 4);
            MapIcon(regional, restTwo.x, restTwo.y, LegendColors[1], 4);
            MapIcon(regional, player.x, player.y, LegendColors[0], 24);
            var portal = RoomCenter(rooms[1]);
            Diamond(regional, "Map transition", portal.x, portal.y, 4, LegendColors[9]);
            var item = RoomCenter(rooms[rooms.Length - 2]);
            Diamond(regional, "Map pickup", item.x, item.y, 5, LegendColors[8]);
            RedrawMarkers();
            ApplyView();
        }

        private static Vector2 RoomCenter(Room room) => new Vector2(room.X + room.W * .5f, room.Y + room.H * .5f);

        private static void DrawRoom(RectTransform root, Room room, int index)
        {
            bool visited = room.Visited;
            var border = visited ? new Color32(88, 167, 204, 212) : new Color32(100, 113, 116, 179);
            var fill = visited ? new Color32(23, 97, 142, 204) : new Color32(48, 55, 60, 201);
            var box = Box(root, "Map room " + (index + 1), room.X, room.Y, room.W, room.H);
            Fill(box, "Room border", 0, 0, room.W, room.H, border);
            Fill(box, "Room fill", 1, 1, room.W - 2, room.H - 2, fill);
            if (room.W > 45)
            {
                Fill(box, "Room floor", 5, room.H - 8, room.W * .45f, 2, border);
                Fill(box, "Room ledge", room.W * .54f, room.H * .44f, room.W * .35f, 2, border);
            }
            if (room.W > 93)
            {
                Fill(box, "Room wall", room.W * .63f, room.H * .42f, 2, room.H * .50f, border);
                Fill(box, "Room upper floor", 10, room.H * .40f, room.W * .29f, 2, border);
            }
        }

        private static void MapIcon(RectTransform parent, float x, float y, Color color, int sides)
        {
            Shape(parent, "Map symbol halo", x - 14, y - 14, 28, 28, sides, 1.7f, new Color32(108, 198, 213, 122));
            Shape(parent, "Map symbol", x - 9, y - 9, 18, 18, sides, 2.5f, color);
            Shape(parent, "Map symbol center", x - 4, y - 4, 8, 8, sides, 0, color);
        }

        private static void Segment(RectTransform root, Vector2 a, Vector2 b, Color color)
        {
            if ((a - b).sqrMagnitude < 1f) return;
            if (Mathf.Abs(a.x - b.x) < 1f)
                Fill(root, "Vertical connection", a.x, Mathf.Min(a.y, b.y), 1.2f, Mathf.Abs(a.y - b.y), color);
            else
                Fill(root, "Horizontal connection", Mathf.Min(a.x, b.x), a.y, Mathf.Abs(a.x - b.x), 1.2f, color);
        }

        private void RedrawMarkers()
        {
            if (pointLayer != null)
            {
                pointLayer.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(pointLayer.gameObject);
            }
            pointLayer = Box(drawing, "Local map pin layer", 0, 0, 1280, 695);
            foreach (var mark in marks[region])
            {
                var x = pointLayer.rect.width * .5f + mark.x;
                var y = pointLayer.rect.height * .5f - mark.y;
                Shape(pointLayer, "Local pin", x - 10, y - 10, 20, 20, 4, 2.6f, new Color32(243, 184, 95, 255));
                Diamond(pointLayer, "Pin core", x, y, 4.5f, new Color32(243, 223, 109, 255));
            }
            counter.text = "◇  " + (15 - marks[region].Count) + " / 15    自订标记";
        }

        private void Place(Vector2 local)
        {
            if (dialog.gameObject.activeSelf) return;
            var position = (local - pan) / zoom;
            if (!canvas.rect.Contains(position)) return;
            var pins = marks[region];
            var match = pins.FindIndex(point => (position - point).sqrMagnitude < 21 * 21);
            if (match >= 0) pins.RemoveAt(match);
            else if (pins.Count < 15) pins.Add(position);
            RedrawMarkers();
        }

        private Vector2 PointInClip(PointerEventData data)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(clip, data.position, data.pressEventCamera, out var point);
            return point - clip.rect.center;
        }

        private void Zoom(float delta, Vector2 screen)
        {
            if (dialog.gameObject.activeSelf || delta == 0f) return;
            var newZoom = Mathf.Clamp(zoom * (delta > 0f ? 1.2f : 1f / 1.2f), .58f, 2.5f);
            pan = screen - (screen - pan) * (newZoom / zoom);
            zoom = newZoom;
            ApplyView();
        }

        private void Pan(Vector2 delta)
        {
            if (dialog.gameObject.activeSelf) return;
            pan += delta;
            ApplyView();
        }

        private void ApplyView()
        {
            pan.x = Mathf.Clamp(pan.x, -580 * zoom, 580 * zoom);
            pan.y = Mathf.Clamp(pan.y, -310 * zoom, 310 * zoom);
            canvas.anchoredPosition = pan;
            canvas.localScale = new Vector3(zoom, zoom, 1);
        }
    }

    internal sealed class MenuMapPointer : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IScrollHandler, IPointerClickHandler
    {
        internal Action<Vector2> Drag;
        internal Action Begin;
        internal Action End;
        internal Action<float> Wheel;
        internal Action<PointerEventData> Click;
        internal Vector2 LastPoint { get; private set; }
        private Vector2 previousDragPoint;

        public void OnBeginDrag(PointerEventData data)
        {
            previousDragPoint = Local(data);
            Begin?.Invoke();
        }

        public void OnDrag(PointerEventData data)
        {
            var next = Local(data);
            Drag?.Invoke(next - previousDragPoint);
            previousDragPoint = next;
        }

        public void OnEndDrag(PointerEventData data) => End?.Invoke();

        public void OnScroll(PointerEventData data)
        {
            LastPoint = Local(data);
            Wheel?.Invoke(data.scrollDelta.y);
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) Click?.Invoke(data);
        }

        private Vector2 Local(PointerEventData data)
        {
            var rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position, data.pressEventCamera, out var point);
            return point - rect.rect.center;
        }
    }
}

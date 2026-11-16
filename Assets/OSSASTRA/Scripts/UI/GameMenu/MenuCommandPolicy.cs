// 解析菜单与游戏同帧按键、窗口优先级及方向重复。
namespace Ossastra.UI.GameMenu
{
    internal enum MenuCancelRoute
    {
        Commerce, MenuAudio, Menu, LegacyMenu, Inventory, Open
    }

    internal static class MenuCommandPolicy
    {
        public static MenuCancelRoute Resolve(bool allowsMenu, bool menuAudio, bool menuOpen,
            bool legacyMenu, bool inventory)
        {
            if (!allowsMenu) return MenuCancelRoute.Commerce;
            if (menuAudio) return MenuCancelRoute.MenuAudio;
            if (menuOpen) return MenuCancelRoute.Menu;
            if (legacyMenu) return MenuCancelRoute.LegacyMenu;
            return inventory ? MenuCancelRoute.Inventory : MenuCancelRoute.Open;
        }

        public static int SignedEdge(bool previous, bool next) =>
            (next ? 1 : 0) - (previous ? 1 : 0);
    }

    internal sealed class MenuNavigationRepeat
    {
        private const float Threshold = .55f;
        private const double InitialDelay = .32;
        private const double RepeatDelay = .105;
        private (int x, int y) direction;
        private bool neutralRequired;
        private double nextRepeat;

        public void RequireNeutral()
        {
            neutralRequired = true;
            direction = default;
            nextRepeat = 0d;
        }

        public (int x, int y) Sample(float x, float y, double now)
        {
            var candidate = Discrete(x, y);
            if (candidate == (0, 0))
            {
                neutralRequired = false;
                direction = default;
                return default;
            }
            if (neutralRequired) return default;
            if (direction != candidate)
            {
                direction = candidate;
                nextRepeat = now + InitialDelay;
                return candidate;
            }
            if (now < nextRepeat) return default;
            nextRepeat = now + RepeatDelay;
            return candidate;
        }

        private static (int x, int y) Discrete(float x, float y)
        {
            var horizontal = System.Math.Abs(x);
            var vertical = System.Math.Abs(y);
            if (System.Math.Max(horizontal, vertical) < Threshold) return default;
            return horizontal >= vertical ? (x > 0 ? 1 : -1, 0) : (0, y > 0 ? 1 : -1);
        }
    }
}

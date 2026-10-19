using Ossastra.CharacterCore;
using UnityEngine;

namespace Ossastra.Development
{
    public sealed class CombatTestTarget : MonoBehaviour
    {
        [SerializeField] private string targetId = "target-a";
        [SerializeField, Min(1)] private int maximumHp = 100;
        [SerializeField, Min(.01f)] private float flashSeconds = .15f;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private float flashUntil;
        private bool flashing;
        public Health Health { get; private set; }

        private void Awake()
        {
            Health = new Health(targetId, maximumHp);
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
        }

        public void ShowHit() { flashing = true; flashUntil = Time.time + flashSeconds; Paint(Color.white); }
        public void RestoreHealth()
        {
            Health = new Health(targetId, maximumHp);
            flashing = false;
            Paint(new Color(.15f, .7f, .7f));
        }

        private void Update()
        {
            if (!flashing || Time.time < flashUntil) return;
            flashing = false;
            Paint(Health.Current == 0 ? Color.gray : new Color(.15f, .7f, .7f));
        }

        private void Paint(Color color)
        {
            if (block == null) return;
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            foreach (var r in renderers) if (r != null) r.SetPropertyBlock(block);
        }

        private void OnGUI()
        {
            var camera = Camera.main;
            if (camera == null || Health == null) return;
            var point = camera.WorldToScreenPoint(transform.position + Vector3.up * 1.8f);
            if (point.z <= 0) return;
            GUI.Box(new Rect(point.x - 70, Screen.height - point.y, 140, 26),
                targetId + "  HP " + Health.Current + "/" + Health.Maximum);
        }
    }
}

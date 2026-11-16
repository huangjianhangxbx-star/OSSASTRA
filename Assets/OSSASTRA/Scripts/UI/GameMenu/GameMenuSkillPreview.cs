// 只在技能页选中对应条目时播放本机的演示视频。
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Ossastra.UI.GameMenu
{
    [RequireComponent(typeof(RawImage), typeof(VideoPlayer))]
    internal sealed class GameMenuSkillPreview : MonoBehaviour
    {
        private RawImage image;
        private VideoPlayer player;

        public void Configure(VideoClip clip)
        {
            image = GetComponent<RawImage>();
            image.raycastTarget = false;
            image.enabled = false;
            player = GetComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = true;
            player.waitForFirstFrame = true;
            player.renderMode = VideoRenderMode.APIOnly;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.clip = clip;
            player.prepareCompleted += PlayPrepared;
        }

        private void OnEnable()
        {
            if (player != null && player.clip != null) player.Prepare();
        }

        private void PlayPrepared(VideoPlayer ready)
        {
            if (isActiveAndEnabled) ready.Play();
        }

        private void LateUpdate()
        {
            if (player == null || player.frame < 0 || player.texture == null) return;
            image.texture = player.texture;
            image.enabled = true;
        }

        private void OnDisable()
        {
            if (player == null) return;
            player.Stop();
            image.enabled = false;
            image.texture = null;
        }

        private void OnDestroy()
        {
            if (player != null) player.prepareCompleted -= PlayPrepared;
        }
    }
}

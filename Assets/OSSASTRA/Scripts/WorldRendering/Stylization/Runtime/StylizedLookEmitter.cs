// Explicit candidate-only luminous geometry. Registration uses the existing Room
// lifecycle; light transport remains ordinary URP lighting, with no visibility grant.
using UnityEngine;
using Ossastra.SceneVisibility.Rooms;
namespace Ossastra.WorldRendering.Stylization
{
 public sealed class StylizedLookEmitter : MonoBehaviour
 {
  public Renderer surface;
  RoomPresentationBinding binding;
  void OnEnable(){if(Application.isPlaying&&surface!=null)binding=RoomPresentationBinding.Attach(surface,transform.position,true,null,true,1);}
  void OnDisable(){if(binding!=null)binding.Suspend();binding=null;}
 }
}

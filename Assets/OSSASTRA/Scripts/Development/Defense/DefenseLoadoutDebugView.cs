/* 脚本：DefenseLoadoutDebugView
 * 功能：新试玩场读取真实HP/架势/体力/机动；提供显式资源边界与恢复调试。
 * 边界：仅开发场装配，不存玩法余额，不强行宣告试炼成功；按钮走原资源入口。
 */
using System;using UnityEngine;using Ossastra.Characters;using Ossastra.Combat;
namespace Ossastra.Development {
public sealed class DefenseLoadoutDebugView:MonoBehaviour {
 private CharacterControlCoordinator session;
 private void Start(){session=FindObjectOfType<CharacterControlCoordinator>();}
 private void OnGUI(){if(session==null)return;var rect=new Rect(12,12,390,235);foreach(var root in session.Characters)root.GetComponent<CharacterInputHandler>().RegisterPointerRegion(this,rect);
 GUILayout.BeginArea(rect,GUI.skin.box);GUILayout.Label("ROADMAP03 · SAMPLE / 正式敌美术ASSET_BLOCKED");GUILayout.Label("WASD移动 / LMB主手 / Shift闪避 / Tab切人");
 foreach(var root in session.Characters){var s=root.GetComponent<CharacterStamina>();var d=root.GetComponent<CharacterDodge>();GUILayout.Label(root.ActorId+(session.Current==root?" [Current]":"")+" HP "+root.Vitals.Health.Current+" 体力 "+(s==null?"无":s.Current.ToString("F1"))+" 闪避 "+(d!=null&&d.IsActive)+" 免伤 "+(d!=null&&d.IsEvading));}
 var c=session.Current;if(c!=null){var s=c.GetComponent<CharacterStamina>();GUILayout.Label(s?.LastPayment??"无体力能力");GUILayout.BeginHorizontal();if(GUILayout.Button(new GUIContent("体力耗尽","开发：真实资源入口扣除当前体力，验证非法请求"))&&s!=null)s.Spend(Guid.NewGuid(),s.Current,c.SimulationTime,"Development-empty");if(GUILayout.Button(new GUIContent("恢复体力","开发：恢复同一本体资源实例，验证合法请求")))s?.Restore();GUILayout.EndHorizontal();}
 GUILayout.Label("状态只读；调试不代表自然路径已通过。");GUILayout.EndArea();}
 private void OnDisable(){if(session!=null)foreach(var root in session.Characters)if(root!=null)root.GetComponent<CharacterInputHandler>().RemovePointerRegion(this);}
}}

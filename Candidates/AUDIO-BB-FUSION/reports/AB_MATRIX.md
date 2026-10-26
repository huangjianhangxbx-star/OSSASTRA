# Controlled A0–A3 experiment

Actual real-input story produced 237 frozen semantic/world facts and 13 BGM context edges. These exact facts were replayed through OSSASTRA's real root, permit pool, Mixer and AudioListener at 48kHz stereo. Replay does not recalculate gameplay damage; initial story separately proves the input-to-fact chain. No Lab offline render is presented as Unity output.

| Mode | Real DSP seconds | Peak | RMS | Clipped samples | Voice peak |
|---|---:|---:|---:|---:|---:|
| A0 | 56.171 | 0.253105 | 0.005294 | 0 | 5 |
| A1 | 56.149 | 0.285224 | 0.014776 | 0 | 5 |
| A2 | 56.149 | 0.243570 | 0.015711 | 0 | 9 |
| A3 | 56.149 | 0.258214 | 0.015623 | 0 | 9 |

A0=current original catalog/semantic rules; A1=BB recordings with original per-event gain/pitch/priority/spacing; A2=BB composition, material/variant and new world facts; A3=same A2 media/semantic/gain/seed plus spatial attenuation, wall lowpass/reverb. Both profile contracts and actual trace confirm shared user gains. Raw loudness differs because media differ; **equal-RMS listening copies** apply only a post-capture scalar to the intact real Mixer recordings, never a gameplay mix change. Matched listening paths and blind order are in local BLIND_LISTENING_INDEX.json.

Source selection seed 27027. Per-event scheduling/selection/refusal CSVs are in ../results. One frame/buffer variation in physical duration is retained, not edited away. Initial 53s input story has 20/21 old assertions: a one-second enemy-hit window did not observe a new loss (190→190), while actual team hurt 200→190 and hurt voices were observed elsewhere. Human judgment remains NEEDS_LISTENING.

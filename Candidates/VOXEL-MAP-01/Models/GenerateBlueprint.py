"""功能：绘制先于模型的三视图与接口蓝图；所有尺寸以米/0.25m格明确，不取参考像素色。"""
from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageFont

OUT = Path('E:/WORLDCREATOR/XingHaiHuiLang/研究/VOXEL-MAP-01/Models')
OUT.mkdir(parents=True, exist_ok=True)
spec = {
 'assetId':'ossastra.timber-house', 'revision':1, 'gridMeters':.25,
 'coordinates':'Unity X-right Y-up Z-back; front=-Z; Blender(x,y,z)->Unity(x,z,-y)',
 'interiorOrigin':[0,0,0], 'wallOuterMeters':[8,3,7], 'interiorClearMeters':[7.5,3,6.5],
 'roofOuterMeters':[9,4.75,8], 'roofEaveY':3, 'roofRidgeY':4.75,
 'door':{'wall':'South','center':[0,1.125,-3.5],'clearMeters':[2,2.25]},
 'windows':[
  {'wall':'West','center':[-4,1.625,0],'clearMeters':[1.5,1.25]},
  {'wall':'East','center':[4,1.625,0],'clearMeters':[1.5,1.25]},
  {'wall':'North','center':[0,1.625,3.5],'clearMeters':[2,1.25]}],
 'structureParts':['Structure_Floor','Structure_WallSouth','Structure_WallNorth','Structure_WallWest','Structure_WallEast'],
 'coverParts':['Cover_RoofSouth','Cover_RoofNorth','Cover_Ridge'],
 'referenceInterpretation':'截图2展示剖开木构室内；层叠挑檐、粗柱、木板中层、灰蓝柱脚可观察，完整屋顶属合理外推。',
 'visibility':'顶盖独立；墙封口留在Structure；前向挡视墙按既有可视权威消费；模型不自带透视或房间逻辑。',
 'materials':{
  'wood.frame':[.28,.18,.105,1], 'wood.panel':[.48,.32,.18,1],
  'wood.floor':[.43,.285,.16,1], 'wood.trim':[.59,.43,.255,1],
  'roof.base':[.29,.31,.34,1], 'roof.edge':[.44,.44,.405,1],
  'stone.base':[.39,.43,.45,1], 'stone.cap':[.49,.52,.52,1]}
}
(OUT/'TimberHouseBlueprint.json').write_text(json.dumps(spec,ensure_ascii=False,indent=2),encoding='utf-8')
W,H=1700,1300
im=Image.new('RGB',(W,H),'#14202b'); d=ImageDraw.Draw(im)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',24)
big=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',35)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',20)
def text(x,y,s,size=24,color='#e3eaf0'): d.text((x,y),s,font=big if size==35 else small if size==20 else font,fill=color)
def rect(box,c,outline='#d2bc8d',width=2):d.rectangle(box,fill=c,outline=outline,width=width)
def grid(x,y,w,h):
 for a in range(x,x+w+1,20): d.line((a,y,a,y+h),fill='#243644')
 for b in range(y,y+h+1,20): d.line((x,b,x+w,b),fill='#243644')
text(35,22,'VOXEL-MAP-01 | 木构主屋三视图 / 建模前蓝图',35)
text(35,73,'基于参考2可观察的檐梁、粗方柱、木板中层与柱脚；完整屋顶为合理外推。',20)
# Front: 80 px/m; wall width 8m, eave 9m; ground y=560.
x,y,s=105,560,80
grid(65,135,760,440)
text(80,112,'正视图 · Front = -Z · 室内地坪Y=0',24)
rect((x,y-240,x+640,y),'#795332')
for xx in [x,x+160,x+440,x+600]: rect((xx,y-240,xx+40,y),'#483522')
for xx in [x,x+600]:
 rect((xx-10,y-45,xx+50,y),'#69777c');rect((xx-15,y-60,xx+55,y-45),'#849094')
rect((x+240,y-180,x+400,y),'#14202b')
rect((x+220,y-200,x+420,y-180),'#a08050')
for level in range(6):
 inset=level*8;top=y-240-level*24
 rect((x-40+inset,top-20,x+680-inset,top),'#67685f' if level%2==0 else '#4d555c')
for xx in range(x+50,x+640,40): d.line((xx,y-232,xx,y-8),fill='#916b42',width=2)
rect((x+240,y-180,x+400,y),'#14202b')
text(x+236,y+18,'净门洞 2.00 × 2.25m',20)
d.line((x,y+65,x+640,y+65),fill='#60cfb9',width=2);text(x+230,y+72,'墙外宽 8.00m',20)
text(80,660,'侧视图 · 层叠挑檐 / 两面独立顶盖',24)
# Side: roof stepped cross-section.
sx,sy,sc=110,1100,72
grid(65,705,760,420)
rect((sx,sy-216,sx+504,sy),'#795332')
for xx in [sx,sx+468]:rect((xx,sy-216,xx+36,sy),'#483522')
rect((sx+198,sy-162,sx+306,sy-72),'#14202b')
rect((sx+184,sy-180,sx+320,sy-162),'#a08050');rect((sx+184,sy-72,sx+320,sy-54),'#a08050')
for z in range(16):
 a=sx-36+z*36;level=min(z,15-z)
 rect((a,sy-216-level*18-18,a+36,sy-216-level*18),'#545c63' if z%2 else '#72756a')
text(sx+142,sy+15,'窗净孔 1.50 × 1.25m',20)
text(sx+150,sy+49,'墙外深 7.00m',20)
text(925,112,'俯视图 · 墙体 / 真实洞口 / 顶盖拆分',24)
# Top plan: 70 px/m.
tx,ty,t=960,200,70
grid(890,145,750,620)
rect((tx-35,ty-35,tx+595,ty+525),'#3a4650',outline='#7ad4ca')
rect((tx,ty,tx+560,ty+490),'#765337')
rect((tx+18,ty+18,tx+542,ty+472),'#283844')
# left/right/back openings in plan; front door at bottom.
rect((tx-2,ty+193,tx+20,ty+298),'#66afba');rect((tx+540,ty+193,tx+562,ty+298),'#66afba')
rect((tx+210,ty-2,tx+350,ty+20),'#66afba')
rect((tx+210,ty+470,tx+350,ty+492),'#14202b')
d.line((tx-35,ty+245,tx+595,ty+245),fill='#d8af66',width=4)
text(tx+45,ty+70,'Cover_RoofNorth',20,color='#9ed9d0')
text(tx+45,ty+375,'Cover_RoofSouth',20,color='#9ed9d0')
text(tx+45,ty+267,'Structure墙顶封口保留',20)
text(tx+175,ty+505,'门朝 -Z / 前立面',20)
text(915,820,'接口与尺度',24)
for i,line in enumerate([
 '人物高1.50m、半径0.22m；门净宽2.00m。',
 '外墙8×7m；内部净空7.5×6.5m；墙高3m。',
 '体素0.25m；檐外伸0.50m；脊顶约4.75m。',
 '顶盖：南坡、北坡、脊冠，统一登记隐藏。',
 '结构：四墙/厚封口、地板/柱脚；室内空置。',
 '侧/背窗是真洞，不用黑片假洞或贴图。',
 '每部件合并网格；原blend保留受管理块。',
 '制作源Z-up；FBX按-Z Forward / Y Up导出。']):text(915,866+i*41,line,20)
text(35,1237,'蓝图是内部审查候选，不是成品或Unity接入完成声明。',20,color='#91a7b6')
im.save(OUT/'TimberHouseBlueprint.png')
# SVG deliberately embeds no third-party image; simple technical geometry remains editable.
svg=['<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="700" viewBox="0 0 1200 700">','<rect width="1200" height="700" fill="#14202b"/>','<g fill="none" stroke="#d2bc8d" stroke-width="2">',
 '<path d="M60 360V150H540V360 M240 360V225H360V360 M30 150H570V130H30Z M45 125H555V105H45Z M60 100H540V80H60Z"/>',
 '<path d="M60 630V450H480V630 M228 570V495H318V570 M30 450L90 450L90 430L150 430L150 410L210 410L210 390H330L330 410L390 410L390 430L450 430L450 450H510"/>',
 '<path d="M700 130H1120V550H963V530H858V550H700Z M718 148H1102V532H963 M858 532H718Z M670 100H1150V580H670Z M670 340H1150"/>','</g>',
 '<g fill="#e3eaf0" font-family="sans-serif" font-size="20"><text x="40" y="35">Timber House | 0.25m voxel | Front / Side / Roof plan</text><text x="60" y="390">Front 8m; door clear 2.00m x 2.25m</text><text x="60" y="665">Side 7m; wall 3m; ridge 4.75m</text><text x="730" y="220">Cover_RoofNorth</text><text x="730" y="450">Cover_RoofSouth</text><text x="730" y="630">Structure caps remain; roof hides separately</text></g></svg>']
(OUT/'TimberHouseBlueprint.svg').write_text('\n'.join(svg),encoding='utf-8')
(OUT/'BlueprintNotes.md').write_text('# 木构主屋蓝图候选\n\n截图2仅给出剖面室内，不显示完整屋顶；本候选以层叠外挑横檐、粗方柱、深梁与木板中层、灰蓝柱脚为可观察识别点，完整浅阶梯坡屋顶为合理外推。\n\n规格见JSON；三视图先审后建模。语义分组以Unity X/Y/Z和门朝-Z为准，Blender内部换轴由导出器负责。内部地坪为原点Y=0，基础向下0.25m。\n\n拟以既有工作台api.apply_recipe创建受管理块，保留原生blend。成品FBX只在导出副本中按语义合并，并经已有工作台api.export_asset走官方FBX导出，避免逐块渲染；不修改工作台本体。\n',encoding='utf-8')
print(OUT)

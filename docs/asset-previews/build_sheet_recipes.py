"""Map all old sheet slices to reusable UI layers. No image pixel changes."""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[2];DOC=ROOT/'docs/asset-previews'
audit=json.loads((DOC/'sheet-source-audit.json').read_text(encoding='utf-8'))
recipes={}
def setr(sheet,name,base,frame='',glyph='',overlay='',special=''):
 recipes[(sheet,name)]=dict(base=base,frame=frame,glyph=glyph,overlay=overlay,special=special)
def s2(n,base,frame='',glyph='',overlay='',special=''):setr('2-Sheet','2-Sheet_'+str(n),base,frame,glyph,overlay,special)
for n in [1,3,7]:s2(n,'WhiteFill')
for n,key in [(0,'Heart'),(2,'Gem'),(6,'Coin')]:s2(n,'Items/'+key)
for n,g in [(8,'BookGlyph'),(9,'CharacterGlyph'),(13,'RankGlyph'),(14,'MailGlyph'),(26,'DocumentGlyph'),(49,'DaggerGlyph'),(61,'HomeGlyph'),(62,'CharacterGlyph')]:s2(n,'GrayFill','SquareFrame',g)
s2(11,'DarkGrayFill','SquareFrame','CharacterGlyph');s2(12,'GrayFill','SquareFrame','MenuGlyph')
s2(22,'WhiteFill','PanelFrame','MapGlyph')
for n,g in [(24,'TicketGlyph'),(25,'CloverGlyph')]:s2(n,'GrayFill','SquareFrame',g,'DisableCross')
for n,g in [(45,'TicketGlyph'),(46,'CloverGlyph'),(54,'CharacterGlyph'),(55,'DaggerGlyph'),(59,'HomeGlyph')]:s2(n,'WhiteFill','SquareFrame',g)
for n,g in [(47,'CharacterGlyph'),(48,'DaggerGlyph')]:s2(n,'WhiteFill','SquareFrame',g,'DisableCross')
s2(60,'GoldFill','RoundedButtonFrame')
for n,key in enumerate(['WallIntact','WallDamaged','WallRubble']):setr('2-Sheet','wall_'+str(n),key,special='world')
for n,key in enumerate(['WhiteFill','SkyBlueFill','OrangeFill']):setr('2-Sheet','gauge_'+str(n),key,'GaugeFrame' if n==0 else '')
for n in range(6):setr('2-Sheet','weapon_'+str(n),'GrayDisk' if n<3 else 'BlueDisk','CircleOutline','Skills/'+['Lightning','Fireball','Arrow'][n%3])
for name in ['card_0','2-Sheet_28','2-Sheet_29']:setr('2-Sheet',name,'LightGrayFill','CardFrame')
for name in ['card_1','2-Sheet_31','2-Sheet_32']:setr('2-Sheet',name,'SkyBlueFill','CardFrame')
for name,key in [('skillIcon_2','Fireball'),('skillIcon_3','Lightning'),('skillIcon_1','Arrow')]:setr('2-Sheet',name,'Skills/'+key)
for name in ['skill_0','skill_1','2-Sheet_57','2-Sheet_58']:setr('2-Sheet',name,'GrayDisk','CircleOutline')
def s3(n,base,frame='',glyph='',overlay='',special=''):setr('3-Sheet','3-Sheet_'+str(n),base,frame,glyph,overlay,special)
setr('3-Sheet','bg_0','LobbySky',overlay='LobbyGrass',special='lawn')
setr('3-Sheet','bg_1','PlayerDummyGlyph');setr('3-Sheet','bg_2','ButterflyGlyph')
for n,g in [(1,'ChevronLeftBlack'),(2,'ChevronRightBlack'),(3,'ChevronLeftWhite'),(4,'ChevronRightWhite')]:s3(n,'BlueFill','ButtonFrame',g)
for n,base in [(5,'GrayFill'),(8,'LightGrayFill'),(10,'GoldFill'),(13,'TealFill'),(34,'OrangeFill')]:s3(n,base,'ButtonFrame')
s3(6,'OrangeStroke');s3(7,'Skills/Fireball');s3(9,'SearchGlyph');s3(12,'TealFill','SquareFrame','BackWhite')
for n,key in [(15,'ChestOpen'),(19,'ChestClosed'),(20,'ChestClosed')]:s3(n,'Items/'+key)
s3(22,'NavyFill');s3(23,'GrayStroke')
for n in [24,25,26]:s3(n,'GreenNode')
s3(27,'GreenLine');s3(28,'GreenLine',special='nodes')
s3(29,'OrangeStroke');s3(30,'DarkGrayFill','SquareFrame','CharacterGlyph');s3(31,'Skills/Fireball');s3(32,'CyanDash');s3(33,'BrownStroke')
def s4(n,base,frame='',glyph='',overlay='',special=''):setr('4-Sheet','4-sheet_'+str(n),base,frame,glyph,overlay,special)
s4(0,'TealStroke');s4(1,'PurpleStroke');s4(2,'GrayDisk',glyph='CloseWhite')
setr('4-Sheet','pause_0','OrangeFill','SquareFrame','PauseWhite')
setr('4-Sheet','fast_0','DarkDisk','CircleOutline','SpeedOneWhite',special='speed')
setr('4-Sheet','fast_1','DarkDisk','CircleOutline','SpeedTwoWhite',special='speed')
s4(4,'CyanFill','CardFrame');s4(5,'PinkFill','CardFrame')
s4(8,'Items/Heart');s4(9,'Items/HeartBundle');s4(10,'DarkGrayFill','SquareFrame','VideoWhite')
s4(11,'SkyBlueFill','ButtonFrame');s4(12,'Items/Gem');s4(13,'GoldFill','ButtonFrame');s4(14,'GoldFill',special='energyWindow')
def s5(n,base,frame='',glyph='',overlay='',special=''):setr('5-Sheet','5-Sheet_'+str(n),base,frame,glyph,overlay,special)
for n,base in [(0,'LightBlueStroke'),(2,'PinkStroke'),(4,'BlueStroke'),(5,'RedStroke'),(8,'OrangeStroke'),(9,'BrownStroke'),(10,'GrayStroke')]:s5(n,base)
s5(1,'GrayFill',overlay='ResultSpotlight',special='spotlight');s5(3,'DarkGrayFill')
s5(6,'TealFill','PanelFrame');s5(7,'DarkGrayFill','SquareFrame','CharacterGlyph')
s5(11,'SkyBlueFill','GaugeFrame');s5(12,'GrayFill','PanelFrame');s5(13,'RedFill','GaugeFrame')
s5(14,'NavyFill','SquareFrame','Items/Coin');s5(15,'BlueFill','ButtonFrame');s5(16,'OrangeFill','ButtonFrame')
setr('6-Sheet','6-sheet_0','NavyFill','SquareFrame');setr('6-Sheet','6-sheet_1','Items/Coin')
output=[]
for sheet in audit:
 name=Path(sheet['path']).stem
 for old in sheet['sprites']:
  assert (name,old['name']) in recipes,(name,old['name'])
  r=dict(sheet=name,name=old['name'],source=sheet['path'],rect=old['rect'],fileID=old['fileID'],guid=sheet['guid'],references=old['references'],**recipes[(name,old['name'])])
  r['prefab']='Assets/_Project/Prefabs/UI/LayeredAssets/'+name+'/'+old['name']+'.prefab'
  output.append(r)
assert len(output)==117
(DOC/'sheet-layer-recipes.json').write_text(json.dumps(output,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(DOC/'sheet-layer-recipes.tsv').write_text('\n'.join('\t'.join(map(str,[r['sheet'],r['name'],r['rect'][2],r['rect'][3],r['base'],r['frame'],r['glyph'],r['overlay'],r['special'],r['prefab']])) for r in output)+'\n',encoding='utf-8')
print('Mapped all117 old slices to layer recipes.')

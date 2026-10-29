using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 public partial class LichChecks {
  static void ShowGeneIconGallery(){
   Find.WindowStack.TryRemove(typeof(ConstructRoleGallery));
   Find.WindowStack.Add(new GeneIconGallery());
  }
  // 1.18.0：牛头人/吉斯洋基新图标与原版同规格图标并排。
  static void ShowMinotaurGithIcons(){
   Find.WindowStack.TryRemove(typeof(CommonRaceGallery));
   var vanilla=new[]{"UI/Icons/Genes/Gene_HeadboneMinihorns","UI/Icons/Genes/Gene_BodyHulk","UI/Icons/Genes/Gene_Furskin","UI/Icons/Genes/Gene_NosePig","UI/Icons/Genes/Gene_EnhancedPsychicAbility","UI/Icons/Genes/Gene_EarPointed","Things/Item/Equipment/WeaponMelee/LongSword","Things/Pawn/Humanlike/HeadAttachments/MiniHorns/MiniHorns_south"};
   var custom=new[]{"MinotaurGith/Genes/Charge","MinotaurGith/Genes/Horns","MinotaurGith/Genes/Maze","MinotaurGith/Genes/Stature","MinotaurGith/Genes/SilverSword","MinotaurGith/Genes/AstralStep","MinotaurGith/Genes/LichDevotee","MinotaurGith/Genes/Nemesis","MinotaurGith/Genes/Snout","MinotaurGith/Abilities/BullRush","MinotaurGith/Abilities/AstralStep","MinotaurGith/Xenotypes/Minotaur","MinotaurGith/Xenotypes/Githyanki","MinotaurGith/Weapons/SilverSword","MinotaurGith/Horns/BullHorns_south","MinotaurGith/Snout/BullSnout_south"};
   Find.WindowStack.Add(new TextureGallery("牛头人 / 吉斯洋基 图标对照（上：原版同规格；下：本模组新图）",vanilla,custom));
  }
 }
 public class TextureGallery : Window {
  readonly string title;readonly string[] top,bottom;
  public TextureGallery(string title,string[] top,string[] bottom){this.title=title;this.top=top;this.bottom=bottom;forcePause=true;absorbInputAroundWindow=true;}
  public override Vector2 InitialSize=>new Vector2(1160,760);
  public override void DoWindowContents(Rect r){
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(10,0,1120,36),title);Text.Font=GameFont.Small;
   for(int i=0;i<top.Length;i++)Cell(new Rect(10+i*140,44,132,160),top[i]);
   for(int i=0;i<bottom.Length;i++)Cell(new Rect(10+(i%8)*140,224+(i/8)*176,132,168),bottom[i]);
  }
  static void Cell(Rect box,string path){
   Widgets.DrawBoxSolid(box,new Color(.18f,.2f,.21f));
   var tex=ContentFinder<Texture2D>.Get(path,false);
   if(tex!=null)GUI.DrawTexture(new Rect(box.x+6,box.y+4,box.width-12,box.width-12),tex,ScaleMode.ScaleToFit,true);
   Text.Font=GameFont.Tiny;Widgets.Label(new Rect(box.x+4,box.y+box.width-6,box.width-8,40),path.Substring(path.LastIndexOf('/')+1));Text.Font=GameFont.Small;
  }
 }
 public class GeneIconGallery : Window {
  static readonly string[] vanilla={"Body_Hulk","Jaw_Heavy","Robust","Brow_Heavy","Skin_Green","Skin_DeepRed"};
  static readonly string[] custom={"LP_LichGene","LP_NecroticTouch","LP_NecroticBody","LP_FireGiantCore","LP_TrollAppearance","LP_TrollRegeneration"};
  public GeneIconGallery(){forcePause=true;absorbInputAroundWindow=true;}
  public override Vector2 InitialSize=>new Vector2(1120,720);
  public override void DoWindowContents(Rect r){
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(10,0,1060,35),"Biotech gene icon comparison");Text.Font=GameFont.Small;
   Widgets.Label(new Rect(14,37,520,25),"Vanilla Biotech");Widgets.Label(new Rect(560,37,520,25),"DND Rimworld");
   for(int i=0;i<6;i++){
    float y=70+i*100;
    DrawGene(new Rect(14,y,520,94),DefDatabase<GeneDef>.GetNamed(vanilla[i]));
    DrawGene(new Rect(560,y,520,94),DefDatabase<GeneDef>.GetNamed(custom[i]));
   }
  }
  static void DrawGene(Rect box,GeneDef gene){
   Widgets.DrawBoxSolid(box,new Color(.18f,.2f,.21f));
   var tex=ContentFinder<Texture2D>.Get(gene.iconPath,false);
   if(tex!=null)GUI.DrawTexture(new Rect(box.x+8,box.y+6,82,82),tex,ScaleMode.ScaleToFit,true);
   Widgets.Label(new Rect(box.x+105,box.y+12,box.width-110,60),gene.defName+"\n"+gene.iconPath);
  }
 }
}

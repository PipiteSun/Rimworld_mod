using Verse;

namespace LichPhylactery {
 // 1.167.0：原在 ForceDragon 里的通用伤害判定（力能龙移除后保留，职业、魔法物品等共用）。
 public static class AttackKind {
  // 爆炸、环境伤害、自伤不算攻击。
  public static bool IsAttack(Pawn p,DamageInfo dinfo)=>dinfo.Def!=null && dinfo.Def.harmsHealth && !dinfo.Def.isExplosive && dinfo.Def.ExternalViolenceFor(p) && dinfo.Instigator!=null && dinfo.Instigator!=p;
  public static bool IsRanged(DamageInfo dinfo)=>dinfo.Def.isRanged || dinfo.Weapon?.IsRangedWeapon==true;
 }
}

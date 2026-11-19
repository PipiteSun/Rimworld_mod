# 1.171.0 移除双头巨人、独眼巨人 报告（2026-11-17）

需求（Peter）：DND边缘大陆里删掉所有双头巨人、独眼巨人的内容，细节自己定。旧存档 11-11 已全部删除，不做存档兼容。

## 先做的事
- 改动前的状态（1.157.0～1.170.0 + 今天的基因图标）已提交并推到 GitHub main：`1477dac`。
- 备份：ArtSource/backup-1.170.0-ettincyclops（受影响的 108 个文件按原路径 + 改动前的 dll）。

## 删掉的
- Defs/HARGiants_Xenotypes.xml 整个文件：异种型 LP_EttinKin / LP_CyclopsKin，本源与能力基因（轮流值守、左右开弓、两头不和、独眼匠心、牧者、单眼视界）、4 个肤色基因、孤首状态与心情、内讧精神状态与崩溃、牧群心情、4 个独眼头型，6 个兵种（两族殖民者、塞伊双头蛮兵、迁徙的流浪双头巨人 / 赶羊独眼巨人、同盟独眼巨人护卫），剧本「山脚的弃儿」。
- Patches/HARGiants_Patches.xml（内讧思考树）；Patches/RedHawk_Xenotypes.xml 去掉两族条目，只留六臂蛇人一条。
- 6 条背景故事（打架双首、丘陵劫掠者、双首守门人、山羊群的牧童、独眼匠人、神谕洞看守）和背景池 LP_BsPool_Ettin / LP_BsPool_Cyclops。
- Defs/Legacy_RaceBodies.xml 里的 LP_EttinBody、LP_CyclopsBody 和部位 LP_EttinHead、LP_CyclopsEye、部位组 LP_EttinSecondHead。
- Textures/HARGiants 全部 49 张图。
- 英文翻译：7 个 DefInjected 文件、LPStrings/HARGiants.xml，以及背景、身体、种族亲和、迁徙里的相关条目。
- 代码：Source/HARGiants.cs、HARGiantsChecks.cs（自检组 GiantKinHAR）、HARGiantsGallery.cs；HAR 联动的 RunGiants。

## 留下 / 改指向
- 通用种族体型 RaceSizeExtension（六臂蛇人在用）移到新文件 Source/RaceSize.cs，行为不变。
- 巨人迁徙：只剩火巨人、食人魔、泰坦、霜巨人、风暴巨人、云巨人六种队伍；原来两族各占的 8% 按原比例回到这六种。
- 塞伊袭击、同盟商队：不再额外插入双头蛮兵 / 独眼护卫，其余兵种照旧。
- 探索公告板的巨人劫掠队：食人魔打手、霜巨人掠夺者、被奴役的风暴巨人。
- 巨人阶序：风暴＞云＞火＞霜。
- 红鹰奴隶池：六臂蛇人的稀有条目保留。
- 去掉的代码引用：夺心魔吞脑（现在一律杀死）、巫妖凝视（不再有凝视免疫）、部位生命的两个专属部位、战棺隐藏第二个头 / 主头偏移、六臂蛇人副手的 needsTwinHeads、巨人装备的 GeneWearExtension（独眼透镜早已移除）、种族加值与种族亲和、旧 HAR 存档里双头 / 独眼的转换。
- 「独眼之神的祭司」（半兽人背景）和委托板里叫「独眼」的头目名字与独眼巨人无关，保留。

## 验证
- Build.ps1 通过（主程序集 + HAR / VPE / 神兽血裔三个联动）。
- 通过：Migration 35、Marilith 19、Backstories 8、AdventurerStage5 12、WarcasketFit（-Warcasket）5，均含存读档。
- Migration 组原来有两条过时的礼物断言（1.146.0 / 1.147.0 改了礼物后没更新：火巨人现在送 100 钢、食人魔送 80 木材），顺手改对了。
- 没通过、和这次删除无关的旧问题（失败点都是以前版本删掉的内容）：Giants（1.146.0 删掉的冬狼皮大氅）、Factions（1.143.0 删掉的圣武士板甲）、Workbenches（已删的巨人铁砧 / 奥术织机）、RedHawk（设置分组数 19 的旧断言）、QuestBoard（读档后狩猎委托一项）。没有修。


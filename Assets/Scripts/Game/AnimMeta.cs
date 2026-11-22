// 由 tools/anim_frames.py 生成，勿手改。
using System.Collections.Generic;

namespace Liangshan
{
    public static class AnimMeta
    {
        /// <summary>
        /// 角色画布中身体（第 0 帧脚底到头顶）占画布边长的比例、脚底离底边的比例、
        /// 两腿分界线的 x（画布比例）、胯部高度（占身体高）。
        /// </summary>
        public static readonly Dictionary<string, (float Body, float Foot, float LegCx, float LegTop)> Info =
            new Dictionary<string, (float, float, float, float)>
        {
            ["en_soldier"] = (0.8219f, 0.0129f, 0.5039f, 0.1600f),
            ["en_shield"] = (0.8351f, 0.0155f, 0.4961f, 0.1600f),
            ["en_cavalry"] = (0.8302f, 0.0142f, 0.5371f, 0.1600f),
            ["en_crossbow"] = (0.8588f, 0.0137f, 0.5186f, 0.1600f),
            ["en_elite"] = (0.7540f, 0.0120f, 0.5000f, 0.2200f),
            ["en_porter"] = (0.7077f, 0.0164f, 0.5020f, 0.1600f),
            ["en_boss"] = (0.8565f, 0.0139f, 0.5000f, 0.2200f),
            ["en_hawk"] = (0.7736f, 0.0142f, 0.5000f, 0.2200f),
            ["en_armored"] = (0.8277f, 0.0146f, 0.4775f, 0.1600f),
            ["en_firebomb"] = (0.5971f, 0.0109f, 0.5000f, 0.2200f),
            ["en_chainhorse"] = (0.8635f, 0.0141f, 0.5000f, 0.2200f),
            ["en_luan"] = (0.7647f, 0.0136f, 0.5163f, 0.1600f),
            ["en_shiwen"] = (0.8035f, 0.0118f, 0.5000f, 0.2200f),
            ["en_huyan"] = (0.7567f, 0.0115f, 0.5195f, 0.1600f),
            ["en_gaoqiu"] = (0.8649f, 0.0131f, 0.5451f, 0.1600f),
            ["en_diver"] = (0.6353f, 0.0134f, 0.5176f, 0.1600f),
            ["en_farmer"] = (0.6690f, 0.0105f, 0.4014f, 0.2284f),
            ["en_hound"] = (0.8613f, 0.0146f, 0.5845f, 0.1614f),
            ["en_rattan"] = (0.8273f, 0.0136f, 0.4697f, 0.1600f),
            ["en_taoist"] = (0.8447f, 0.0137f, 0.5000f, 0.2200f),
            ["en_berserker"] = (0.7376f, 0.0142f, 0.5078f, 0.1600f),
            ["en_catapult"] = (0.6407f, 0.0167f, 0.6006f, 0.2451f),
            ["en_lantern"] = (0.8126f, 0.0126f, 0.5000f, 0.2200f),
            ["en_scout"] = (0.6445f, 0.0128f, 0.5028f, 0.1600f),
            ["en_brute"] = (0.7021f, 0.0155f, 0.5000f, 0.2200f),
            ["en_hetao"] = (0.8089f, 0.0140f, 0.4538f, 0.1600f),
            ["en_zhucf"] = (0.6966f, 0.0158f, 0.5000f, 0.2200f),
            ["en_zengkui"] = (0.7633f, 0.0103f, 0.6035f, 0.1643f),
            ["en_hantao"] = (0.8650f, 0.0133f, 0.4108f, 0.1600f),
            ["en_liuml"] = (0.8692f, 0.0089f, 0.4118f, 0.1600f),
            ["en_waterchief"] = (0.7495f, 0.0123f, 0.4951f, 0.1600f),
            ["en_crossbowcapt"] = (0.8607f, 0.0135f, 0.5049f, 0.1600f),
            ["en_goldeneagle"] = (0.8655f, 0.0126f, 0.5000f, 0.2200f),
            ["en_ironpagoda"] = (0.7439f, 0.0123f, 0.5000f, 0.2200f),
            ["en_highpriest"] = (0.7328f, 0.0129f, 0.5000f, 0.2200f),
            ["en_giantbrute"] = (0.7299f, 0.0142f, 0.5000f, 0.2200f),
            ["en_firecart"] = (0.7406f, 0.0151f, 0.5000f, 0.2200f),
            ["hero_huarong"] = (0.6396f, 0.0203f, 0.4141f, 0.2610f),
            ["hero_lingzhen"] = (0.6397f, 0.0129f, 0.5000f, 0.2200f),
            ["hero_gongsun"] = (0.6396f, 0.0125f, 0.4365f, 0.3200f),
            ["hero_wusong"] = (0.6395f, 0.0163f, 0.5830f, 0.3191f),
            ["hero_linchong"] = (0.4517f, 0.0164f, 0.4023f, 0.1600f),
            ["hero_luzhishen"] = (0.6397f, 0.0141f, 0.5000f, 0.2200f),
            ["hero_zhangqing"] = (0.6401f, 0.0148f, 0.5000f, 0.2200f),
            ["hero_wuyong"] = (0.6405f, 0.0131f, 0.5000f, 0.2200f),
            ["hero_lujunyi"] = (0.4499f, 0.0164f, 0.4092f, 0.1600f),
            ["hero_guansheng"] = (0.6397f, 0.0129f, 0.3965f, 0.1600f),
            ["hero_yanqing"] = (0.6398f, 0.0166f, 0.5742f, 0.3200f),
            ["hero_fanrui"] = (0.6399f, 0.0149f, 0.4346f, 0.3200f),
            ["hero_husanniang"] = (0.6401f, 0.0145f, 0.3975f, 0.3200f),
            ["hero_andaoquan"] = (0.6396f, 0.0141f, 0.5000f, 0.2200f),
            ["hero_daizong"] = (0.6400f, 0.0145f, 0.3867f, 0.3200f),
            ["hero_qinming"] = (0.6116f, 0.0165f, 0.4150f, 0.3200f),
            ["hero_likui"] = (0.6404f, 0.0175f, 0.5100f, 0.1600f),
            ["hero_qiaodaoqing"] = (0.5632f, 0.0135f, 0.5728f, 0.3200f),
            ["hero_zhuwu"] = (0.6340f, 0.0186f, 0.3984f, 0.2753f),
            ["hero_xuning"] = (0.6403f, 0.0132f, 0.5000f, 0.2200f),
            ["hero_shantinggui"] = (0.5958f, 0.0129f, 0.5000f, 0.2200f),
            ["hero_weidingguo"] = (0.6396f, 0.0191f, 0.5000f, 0.2200f),
            ["hero_shixiu"] = (0.6401f, 0.0145f, 0.4136f, 0.3200f),
            ["hero_xiezhen"] = (0.6403f, 0.0127f, 0.5869f, 0.2732f),
            ["hero_xiebao"] = (0.6383f, 0.0189f, 0.5000f, 0.2200f),
            ["hero_maling"] = (0.6398f, 0.0202f, 0.4160f, 0.2549f),
            ["hero_kongliang"] = (0.6396f, 0.0141f, 0.5000f, 0.2200f),
            ["hero_tanglong"] = (0.6404f, 0.0154f, 0.5000f, 0.2200f),
            ["hero_sunerniang"] = (0.6402f, 0.0152f, 0.4238f, 0.3200f),
            ["hero_ruanxiaoqi"] = (0.6399f, 0.0149f, 0.5000f, 0.2200f),
            ["hero_shiqian"] = (0.6402f, 0.0163f, 0.5000f, 0.2200f),
            ["hero_jiangjing"] = (0.6404f, 0.0150f, 0.4180f, 0.2335f),
        };

        public const int Frames = 4;

        /// <summary>走路序列帧的基准帧率，和展示台 code_2 的 fps 相同。实际播放按移动速度对步；比它慢的（首领、精英）取对步帧率和它的几何平均。</summary>
        public const float WalkFps = 6f;

        /// <summary>再快 8 张图就糊了。当前最快的火药兵按步幅大约 20 帧/秒，上限留在这之上。</summary>
        public const float WalkFpsMax = 24f;
    }
}

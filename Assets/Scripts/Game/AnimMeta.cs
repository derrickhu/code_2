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
            ["en_soldier"] = (0.8609f, 0.0106f, 0.3916f, 0.2477f),
            ["en_shield"] = (0.8625f, 0.0112f, 0.5767f, 0.1929f),
            ["en_cavalry"] = (0.8088f, 0.0088f, 0.5967f, 0.2771f),
            ["en_crossbow"] = (0.8715f, 0.0100f, 0.5054f, 0.2271f),
            ["en_elite"] = (0.7818f, 0.0103f, 0.5000f, 0.2200f),
            ["en_porter"] = (0.8540f, 0.0103f, 0.4482f, 0.2354f),
            ["en_boss"] = (0.7338f, 0.0097f, 0.5000f, 0.2200f),
            ["hero_huarong"] = (0.8672f, 0.0117f, 0.5000f, 0.1600f),
            ["hero_lingzhen"] = (0.8673f, 0.0122f, 0.4995f, 0.2234f),
            ["hero_gongsun"] = (0.7417f, 0.0105f, 0.5000f, 0.2200f),
            ["hero_wusong"] = (0.8722f, 0.0096f, 0.4333f, 0.2742f),
            ["hero_linchong"] = (0.8715f, 0.0104f, 0.5000f, 0.2025f),
            ["hero_luzhishen"] = (0.7953f, 0.0094f, 0.4961f, 0.1753f),
            ["hero_zhangqing"] = (0.8723f, 0.0100f, 0.4873f, 0.2159f),
            ["hero_wuyong"] = (0.8010f, 0.0097f, 0.5039f, 0.2841f),
            ["en_hawk"] = (0.7712f, 0.0142f, 0.4561f, 0.1600f),
            ["en_armored"] = (0.8028f, 0.0104f, 0.5000f, 0.2200f),
            ["en_firebomb"] = (0.8033f, 0.0100f, 0.4775f, 0.2422f),
            ["en_chainhorse"] = (0.8408f, 0.0090f, 0.5000f, 0.2200f),
            ["en_luan"] = (0.7821f, 0.0101f, 0.4329f, 0.2700f),
            ["en_shiwen"] = (0.8294f, 0.0110f, 0.4512f, 0.2230f),
            ["en_huyan"] = (0.8514f, 0.0110f, 0.4723f, 0.2445f),
            ["en_gaoqiu"] = (0.8690f, 0.0116f, 0.5000f, 0.2200f),
        };

        public const int Frames = 4;
    }
}

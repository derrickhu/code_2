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
            ["hero_lujunyi"] = (0.8692f, 0.0110f, 0.5000f, 0.2200f),
            ["hero_guansheng"] = (0.8366f, 0.0097f, 0.5013f, 0.1600f),
            ["hero_yanqing"] = (0.8710f, 0.0102f, 0.5104f, 0.1600f),
            ["hero_fanrui"] = (0.7849f, 0.0110f, 0.5013f, 0.1929f),
            ["hero_husanniang"] = (0.8587f, 0.0107f, 0.5156f, 0.1600f),
            ["hero_andaoquan"] = (0.8058f, 0.0097f, 0.5000f, 0.2200f),
            ["hero_daizong"] = (0.8260f, 0.0094f, 0.5117f, 0.1730f),
            ["hero_qinming"] = (0.8687f, 0.0113f, 0.4909f, 0.2302f),
            ["hero_likui"] = (0.7355f, 0.0097f, 0.4961f, 0.3089f),
            ["hero_qiaodaoqing"] = (0.8696f, 0.0107f, 0.5000f, 0.2200f),
            ["hero_zhuwu"] = (0.7508f, 0.0097f, 0.3939f, 0.3200f),
            ["hero_xuning"] = (0.8503f, 0.0107f, 0.4961f, 0.1972f),
            ["hero_shantinggui"] = (0.8709f, 0.0103f, 0.4941f, 0.1997f),
            ["hero_weidingguo"] = (0.8696f, 0.0113f, 0.5052f, 0.2002f),
            ["hero_shixiu"] = (0.8636f, 0.0108f, 0.3880f, 0.3200f),
            ["hero_xiezhen"] = (0.8702f, 0.0105f, 0.5111f, 0.2237f),
            ["hero_xiebao"] = (0.7991f, 0.0091f, 0.5124f, 0.3200f),
            ["hero_maling"] = (0.7715f, 0.0099f, 0.4974f, 0.1953f),
            ["hero_kongliang"] = (0.8685f, 0.0111f, 0.4961f, 0.2512f),
            ["hero_tanglong"] = (0.7083f, 0.0089f, 0.4922f, 0.2301f),
            ["hero_sunerniang"] = (0.8715f, 0.0100f, 0.5000f, 0.2200f),
            ["hero_ruanxiaoqi"] = (0.8693f, 0.0098f, 0.4010f, 0.3200f),
            ["hero_shiqian"] = (0.8641f, 0.0112f, 0.4642f, 0.2796f),
            ["hero_jiangjing"] = (0.8696f, 0.0113f, 0.4779f, 0.3023f),
        };

        public const int Frames = 4;
    }
}

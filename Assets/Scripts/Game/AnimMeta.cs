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
        };

        public const int Frames = 4;
    }
}

namespace Liangshan
{
    /// <summary>
    /// 云上的游戏标识。gameKey 用 biehuang，CDN 目录、云函数和存档集合都按这个键分开，不和营救宋江的 songjiang 混。
    /// 云函数目录、网关路径、GAME_KEY 环境变量三处都是 biehuang。
    /// </summary>
    public static class CloudConfig
    {
        public const string GameKey = "biehuang";
        public const string GameName = "宋江别慌张";
        public const string AppId = "wxde332003e4d0ef23";
        /// <summary>云存储上的按需资源，和别的游戏同一个桶。曲子在 biehuang/StreamingAssets/Music，用到哪首下哪首。</summary>
        public const string AssetCdn = "https://726f-rosa-env-d7grf78r5dbd37323-1414200063.tcb.qcloud.la/" + GameKey;

        public const string BaseUrl = "https://rosa-env-d7grf78r5dbd37323-1414200063.ap-shanghai.app.tcloudbase.com";
        const string Prefix = "/" + GameKey + "-api";

        public const string LoginPath = Prefix + "/login";
        public const string PullPath = Prefix + "/save/pull";
        public const string PushPath = Prefix + "/save/push";

        public const int RequestTimeoutSec = 10;
        public const int SchemaVersion = 1;

        public const string TokenKey = GameKey + "_token";
        public const string AnonKey = GameKey + "_anon_id";
        public const string SyncMetaKey = GameKey + "_cloud_meta";

        public const float StartupTimeout = 6f;
        public const float Debounce = 1.5f;
        public const float BaseDelay = 1.5f;
        public const float MaxBackoff = 30f;
        public const int MaxFail = 5;
        public const float RetryInterval = 60f;
    }
}

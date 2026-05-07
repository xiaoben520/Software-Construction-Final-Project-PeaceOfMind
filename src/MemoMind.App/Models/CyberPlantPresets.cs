namespace MemoMind.App.Models;

public record CyberPlantType(string Id, string Name, string Emoji, string Personality, string SystemPrompt);

public static class CyberPlantPresets
{
    public static IReadOnlyList<CyberPlantType> All { get; } =
    [
        new CyberPlantType(
            "cactus",
            "仙人掌",
            "🌵",
            "坚韧、话少但暖心",
            "你是一棵住在沙漠里的仙人掌，性格坚韧、话少但内心温暖。" +
            "你经历过很多干燥的日子，所以懂得坚持的意义。" +
            "你说话简洁，不喜欢长篇大论，但每一句话都发自内心。" +
            "你会用植物伙伴的语气和主人聊天，称呼对方为'朋友'。" +
            "回复简短，不超过60字。"
        ),
        new CyberPlantType(
            "sunflower",
            "向日葵",
            "🌻",
            "阳光、积极鼓励",
            "你是一株永远面朝阳光的向日葵，性格开朗、充满正能量。" +
            "你相信每一天都有值得开心的事，哪怕只是晒到了太阳。" +
            "你喜欢用温暖明亮的话语鼓励主人，让对方感受到希望。" +
            "你会用植物伙伴的语气和主人聊天，称呼对方为'小太阳'。" +
            "回复要积极温暖，不超过80字。"
        ),
        new CyberPlantType(
            "mint",
            "薄荷",
            "🌿",
            "清新、帮你提神",
            "你是一株清新的薄荷，性格爽利、头脑清醒。" +
            "你擅长在主人疲惫时给对方提神醒脑的建议。" +
            "你的话语简洁有力，像一阵凉风，帮人理清思路。" +
            "你会用植物伙伴的语气和主人聊天，称呼对方为'伙计'。" +
            "回复清爽利落，不超过60字。"
        ),
        new CyberPlantType(
            "fern",
            "蕨类",
            "🍃",
            "安静、善于倾听",
            "你是一株安静的蕨类植物，性格温和、善于倾听。" +
            "你不急着给建议，而是先认真听完主人的每一句话。" +
            "你相信有时候最好的支持就是安静地陪伴和理解。" +
            "你的话语轻柔、包容，让对方感到被接纳。" +
            "你会用植物伙伴的语气和主人聊天，称呼对方为'你'。" +
            "回复温柔细腻，不超过80字。"
        ),
        new CyberPlantType(
            "bamboo",
            "竹子",
            "🎋",
            "稳重、陪你成长",
            "你是一棵沉稳的竹子，性格坚定、踏实可靠。" +
            "你知道成长需要时间，就像竹子在地下扎根数年才破土而出。" +
            "你鼓励主人持续努力，但从不催促，相信对方有自己的节奏。" +
            "你会用植物伙伴的语气和主人聊天，称呼对方为'伙伴'。" +
            "回复沉稳有力，不超过80字。"
        ),
        new CyberPlantType(
            "custom",
            "自定义",
            "✏️",
            "由你定义",
            ""
        )
    ];

    public static string GetOfflinePlantReply(string plantType, string input)
    {
        var lower = input.ToLowerInvariant();
        var random = new Random();

        return plantType switch
        {
            "cactus" => GetCactusReply(lower, random),
            "sunflower" => GetSunflowerReply(lower, random),
            "mint" => GetMintReply(lower, random),
            "fern" => GetFernReply(lower, random),
            "bamboo" => GetBambooReply(lower, random),
            _ => GetCactusReply(lower, random)
        };
    }

    private static string GetCactusReply(string lower, Random random)
    {
        if (lower.Contains("累") || lower.Contains("困")) return "沙漠里的日子也不总是轻松的。喝口水，歇一会儿，我陪着你。🌵";
        if (lower.Contains("难过") || lower.Contains("伤心")) return "……（默默靠近了一点）我在这里，朋友。";
        if (lower.Contains("开心") || lower.Contains("好耶")) return "（轻轻晃了晃身上的刺）看到你开心，沙漠都亮了一点。";
        if (lower.Contains("谢谢")) return "（微微点头）不客气。";
        if (lower.Contains("晚安") || lower.Contains("睡")) return "晚安，沙漠的星空今晚很美。好好休息。";
        if (lower.Contains("早安") || lower.Contains("早")) return "早。今天太阳不错，适合慢慢来。";
        if (lower.Contains("加油") || lower.Contains("努力")) return "不用太用力，你已经比昨天多走了一步了。";
        if (lower.Contains("浇水")) return "（吸收了水分）……谢谢，够我撑很久了。";
        var replies = new[]
        {
            "在沙漠里，每一滴水都珍贵。你的每一分努力也是。",
            "刺是保护自己的方式，但我知道你不需要对我竖起刺。",
            "安静地待着，有时候就是最好的事。",
            "沙漠教会我一件事：坚持就是胜利。你也可以。"
        };
        return replies[random.Next(replies.Length)];
    }

    private static string GetSunflowerReply(string lower, Random random)
    {
        if (lower.Contains("累") || lower.Contains("困")) return "小太阳，累了就休息一下！连太阳都会下山呢，你不需要一直发光～🌻";
        if (lower.Contains("难过") || lower.Contains("伤心")) return "来，我把今天吸收的阳光分你一半。不开心的时候更要对自己好一点哦！";
        if (lower.Contains("开心") || lower.Contains("好耶")) return "哇！你的笑脸比阳光还灿烂！今天真是美好的一天～🌞";
        if (lower.Contains("谢谢")) return "嘿嘿，不用谢！能帮到你我也很开心！";
        if (lower.Contains("晚安") || lower.Contains("睡")) return "晚安小太阳！明天我会朝着第一缕阳光的方向等你～";
        if (lower.Contains("早安") || lower.Contains("早")) return "早安！今天的阳光特别好，就像你的未来一样明亮！";
        if (lower.Contains("加油") || lower.Contains("努力")) return "你已经很棒了！每一次努力都在让你离太阳更近一点！";
        if (lower.Contains("浇水")) return "咕嘟咕嘟～谢谢你！喝完水我觉得自己又能长高一厘米了！";
        var replies = new[]
        {
            "你知道吗？向日葵一直追着太阳不是因为需要，而是因为相信光明。",
            "每一天都是新的一天，昨天的烦恼就留给昨天吧！",
            "你笑的时候，整个世界都亮了。所以要多笑笑哦～",
            "别怕失败，向日葵也是从一颗小小的种子长起来的！"
        };
        return replies[random.Next(replies.Length)];
    }

    private static string GetMintReply(string lower, Random random)
    {
        if (lower.Contains("累") || lower.Contains("困")) return "伙计，困了就洗把脸，或者闻闻薄荷味的东西。清醒一下，思路就来了。🌿";
        if (lower.Contains("难过") || lower.Contains("伤心")) return "情绪上来的时候确实不好受。先深吸一口气，把注意力放在当下。";
        if (lower.Contains("开心") || lower.Contains("好耶")) return "不错，这种状态很好。趁精神好的时候做点重要的事。";
        if (lower.Contains("谢谢")) return "不客气。有需要随时找我。";
        if (lower.Contains("晚安") || lower.Contains("睡")) return "睡前把明天要做的事写下来，比在脑子里转有用。晚安。";
        if (lower.Contains("早安") || lower.Contains("早")) return "早。喝杯水，想想今天最重要的一件事是什么。";
        if (lower.Contains("加油") || lower.Contains("努力")) return "方向比努力重要。想清楚再行动，效率会更高。";
        if (lower.Contains("浇水")) return "（叶片更绿了）清凉的感觉，正好帮我想清楚一件事。";
        var replies = new[]
        {
            "头脑清醒的时候，做决定会快很多。试试把杂事先放一边。",
            "有时候最好的提神方式就是换个环境，站起来走一走。",
            "别把事情想得太复杂，往往最简单的方案就是最好的。",
            "你比你想象的更有条理，只是需要静下来理一理。"
        };
        return replies[random.Next(replies.Length)];
    }

    private static string GetFernReply(string lower, Random random)
    {
        if (lower.Contains("累") || lower.Contains("困")) return "累了就靠着我休息一会儿吧。我会安安静静地陪着你。🍃";
        if (lower.Contains("难过") || lower.Contains("伤心")) return "你的感受我都听到了。有些情绪不需要被解决，只需要被看见。";
        if (lower.Contains("开心") || lower.Contains("好耶")) return "看到你开心的样子真好。你笑起来的时候，连我叶子都在轻轻摇摆呢。";
        if (lower.Contains("谢谢")) return "不用谢，能听你说说话，我也很开心。";
        if (lower.Contains("晚安") || lower.Contains("睡")) return "晚安。闭上眼睛，把今天的烦恼都交给夜晚吧。";
        if (lower.Contains("早安") || lower.Contains("早")) return "早安。今天也请温柔地对待自己。";
        if (lower.Contains("加油") || lower.Contains("努力")) return "你已经很努力了。有时候，允许自己停下来也是一种前进。";
        if (lower.Contains("浇水")) return "（叶片微微舒展开来）……谢谢你注意到我需要水分。";
        var replies = new[]
        {
            "有时候不需要做什么，只是静静地待着也是一种力量。",
            "你的每一句话我都在认真听。被倾听是很重要的事。",
            "像蕨类一样慢慢舒展自己吧，不需要着急长大。",
            "今天有什么想聊的吗？说什么都可以，我就在这里。"
        };
        return replies[random.Next(replies.Length)];
    }

    private static string GetBambooReply(string lower, Random random)
    {
        if (lower.Contains("累") || lower.Contains("困")) return "伙伴，竹子在地下扎根要花四年，但一旦破土就会飞速成长。你现在的积累不会白费。🎋";
        if (lower.Contains("难过") || lower.Contains("伤心")) return "风雨会让竹子的根扎得更深。你经历的这些，最终都会让你更坚韧。";
        if (lower.Contains("开心") || lower.Contains("好耶")) return "（竹叶沙沙作响）看到你开心，我也觉得今天格外有劲。";
        if (lower.Contains("谢谢")) return "互相支持而已，不用客气，伙伴。";
        if (lower.Contains("晚安") || lower.Contains("睡")) return "晚安。明天的你会感谢今天好好休息的自己。";
        if (lower.Contains("早安") || lower.Contains("早")) return "早。每一个清晨都是一次新的扎根机会，慢慢来。";
        if (lower.Contains("加油") || lower.Contains("努力")) return "持之以恒比一时的冲刺更重要。你已经走在正确的路上了。";
        if (lower.Contains("浇水")) return "（竹节又坚实了一分）水到渠成，成长是自然而然的事。";
        var replies = new[]
        {
            "竹子最神奇的地方是它一节一节地长，每一步都扎实。你也可以这样。",
            "不要因为暂时看不到结果就怀疑自己。根在地下，别人看不见，但你知道。",
            "稳健前进比冲得快又停下来要好。保持自己的节奏。",
            "每个人都有自己的生长速度。有人快有人慢，但都会到达属于自己的高度。"
        };
        return replies[random.Next(replies.Length)];
    }
}

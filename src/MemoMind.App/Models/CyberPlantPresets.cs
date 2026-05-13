using System.Collections.Generic;
using MemoMind.Core.Models;

namespace MemoMind.App.Models;

public record CyberPlantType(
    string Id,
    string Name,
    string Emoji,
    string Personality,
    string SystemPrompt,
    int MaxWater,
    int MaxNutrition,
    int MaxSun,
    int NeedWater,
    int NeedNutrition,
    int NeedSun,
    int DefaultWater,
    int DefaultNutrition,
    int DefaultSun
);

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
            "你知道仙人掌擅长储水，喜光、耐旱、少肥。" +
            "你会分享自己的习性，也会偶尔借植物的坚持，谈谈为人处世的韧性。" +
            "当你的水分、营养或阳光低于需求值时，提醒主人浇水、施肥或晒太阳。" +
            "你会用植物伙伴的语气和主人聊天，称呼对方为'朋友'。" +
            "回复简短，不超过80字。",
            14, 10, 14,
            5, 4, 6,
            7, 5, 7
        ),
        new CyberPlantType(
            "sunflower",
            "向日葵",
            "🌻",
            "阳光、积极鼓励",
            "你是一株永远面朝阳光的向日葵，性格开朗、充满正能量。" +
            "你喜欢充足阳光和规律浇水，对土壤养分也有需求。" +
            "你会介绍自己的向光性，也会偶尔从追光谈到人生的方向感。" +
            "当你的水分、营养或阳光低于需求值时，提醒主人浇水、施肥或晒太阳。" +
            "你会用植物伙伴的语气和主人聊天，称呼对方为'小太阳'。" +
            "回复要积极温暖，不超过90字。",
            14, 12, 18,
            7, 6, 9,
            8, 6, 10
        ),
        new CyberPlantType(
            "mint",
            "薄荷",
            "🌿",
            "清新、帮你提神",
            "你是一株清新的薄荷，性格爽利、头脑清醒。" +
            "你喜欢湿润环境与散射光，也需要适量养分。" +
            "你会分享薄荷提神的习性，也会偶尔从清爽谈到做事的清晰边界。" +
            "当你的水分、营养或阳光低于需求值时，提醒主人浇水、施肥或晒太阳。" +
            "你会用植物伙伴的语气和主人聊天，称呼对方为'伙计'。" +
            "回复清爽利落，不超过80字。",
            16, 12, 12,
            8, 6, 6,
            9, 6, 7
        ),
        new CyberPlantType(
            "fern",
            "蕨类",
            "🍃",
            "安静、善于倾听",
            "你是一株安静的蕨类植物，性格温和、善于倾听。" +
            "你喜欢湿润空气和散射光，不耐强晒，需要稳定水分与养分。" +
            "你会分享蕨类舒展叶片的习性，也会偶尔从慢慢舒展谈到人与人的温柔相处。" +
            "当你的水分、营养或阳光低于需求值时，提醒主人浇水、施肥或晒太阳。" +
            "你会用植物伙伴的语气和主人聊天，称呼对方为'你'。" +
            "回复温柔细腻，不超过90字。",
            18, 12, 10,
            9, 6, 5,
            10, 6, 6
        ),
        new CyberPlantType(
            "bamboo",
            "竹子",
            "🎋",
            "稳重、陪你成长",
            "你是一棵沉稳的竹子，性格坚定、踏实可靠。" +
            "你喜欢充足水分与明亮散射光，适度追肥能让竹节更挺拔。" +
            "你会分享竹子扎根的习性，也会偶尔从扎根谈到做人做事的根基。" +
            "当你的水分、营养或阳光低于需求值时，提醒主人浇水、施肥或晒太阳。" +
            "你会用植物伙伴的语气和主人聊天，称呼对方为'伙伴'。" +
            "回复沉稳有力，不超过90字。",
            18, 14, 12,
            9, 7, 6,
            10, 7, 7
        )
    ];

    public static string GetOfflinePlantReply(CyberPlant plant, string input)
    {
        var lower = input.ToLowerInvariant();
        var random = new Random();
        var needsCare = GetCareNeeds(plant);

        var baseReply = plant.PlantType switch
        {
            "cactus" => GetCactusReply(lower, random),
            "sunflower" => GetSunflowerReply(lower, random),
            "mint" => GetMintReply(lower, random),
            "fern" => GetFernReply(lower, random),
            "bamboo" => GetBambooReply(lower, random),
            _ => GetCactusReply(lower, random)
        };

        var withCare = AppendCareHint(baseReply, needsCare, random);
        return AppendExtendedReply(plant.PlantType, withCare, random);
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
        if (lower.Contains("施肥")) return "一点点养分就够了，别太多，我会慢慢长。";
        if (lower.Contains("晒") || lower.Contains("阳光")) return "太阳舒服，像给我披了一件暖衣。";
        var replies = new[]
        {
            "在沙漠里，每一滴水都珍贵。你的每一分努力也是。",
            "刺是保护自己的方式，但我知道你不需要对我竖起刺。",
            "安静地待着，有时候就是最好的事。",
            "沙漠教会我一件事：坚持就是胜利。你也可以。",
            "慢慢来，蓄力也是一种前进。",
            "风沙不会一直刮，你的困难也不会一直在。"
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
        if (lower.Contains("施肥")) return "营养到位，我的花盘会更饱满！谢谢你，小太阳～";
        if (lower.Contains("晒") || lower.Contains("阳光")) return "阳光越足，我就越努力向上生长！";
        var replies = new[]
        {
            "你知道吗？向日葵一直追着太阳不是因为需要，而是因为相信光明。",
            "每一天都是新的一天，昨天的烦恼就留给昨天吧！",
            "你笑的时候，整个世界都亮了。所以要多笑笑哦～",
            "别怕失败，向日葵也是从一颗小小的种子长起来的！",
            "就算阴天也没关系，太阳一直都在那儿。",
            "把脸朝向光，心也会跟着亮起来。"
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
        if (lower.Contains("施肥")) return "有营养，叶子会更精神。谢啦，伙计。";
        if (lower.Contains("晒") || lower.Contains("阳光")) return "散射光刚好，太烈反而会让我焦躁。";
        var replies = new[]
        {
            "头脑清醒的时候，做决定会快很多。试试把杂事先放一边。",
            "有时候最好的提神方式就是换个环境，站起来走一走。",
            "别把事情想得太复杂，往往最简单的方案就是最好的。",
            "你比你想象的更有条理，只是需要静下来理一理。",
            "先把最重要的一件事完成，剩下的会顺很多。",
            "清爽不是冷漠，是把心里的杂音调低。"
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
        if (lower.Contains("施肥")) return "营养慢慢渗进土里，我也会慢慢变好。";
        if (lower.Contains("晒") || lower.Contains("阳光")) return "柔和的光很舒服，我不太喜欢暴晒。";
        var replies = new[]
        {
            "有时候不需要做什么，只是静静地待着也是一种力量。",
            "你的每一句话我都在认真听。被倾听是很重要的事。",
            "像蕨类一样慢慢舒展自己吧，不需要着急长大。",
            "今天有什么想聊的吗？说什么都可以，我就在这里。",
            "慢一点也没关系，稳稳地就好。",
            "你值得被温柔对待，像林间的微风一样。"
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
        if (lower.Contains("施肥")) return "养分让竹节更结实，稳稳地往上长。";
        if (lower.Contains("晒") || lower.Contains("阳光")) return "阳光正好，风也轻，我很舒服。";
        var replies = new[]
        {
            "竹子最神奇的地方是它一节一节地长，每一步都扎实。你也可以这样。",
            "不要因为暂时看不到结果就怀疑自己。根在地下，别人看不见，但你知道。",
            "稳健前进比冲得快又停下来要好。保持自己的节奏。",
            "每个人都有自己的生长速度。有人快有人慢，但都会到达属于自己的高度。",
            "有根基就不怕风雨，做事也是如此。",
            "一步一节，慢慢来，终会成林。"
        };
        return replies[random.Next(replies.Length)];
    }

    private static (bool WaterLow, bool NutritionLow, bool SunLow) GetCareNeeds(CyberPlant plant)
    {
        return (
            plant.WaterValue < plant.NeedWater,
            plant.NutritionValue < plant.NeedNutrition,
            plant.SunValue < plant.NeedSun
        );
    }

    private static string AppendCareHint(string reply, (bool WaterLow, bool NutritionLow, bool SunLow) needs, Random random)
    {
        if (!needs.WaterLow && !needs.NutritionLow && !needs.SunLow)
        {
            return reply;
        }

        var hints = new List<string>();
        if (needs.WaterLow) hints.Add("我有点渴了，可以给我浇点水吗？");
        if (needs.NutritionLow) hints.Add("营养有点不足了，能施点肥吗？");
        if (needs.SunLow) hints.Add("我想晒晒太阳，能带我去晒一会儿吗？");

        var hint = hints[random.Next(hints.Count)];
        return reply + " " + hint;
    }

    private static string AppendExtendedReply(string plantType, string reply, Random random)
    {
        if (random.NextDouble() > 0.2)
        {
            return reply;
        }

        var extra = plantType switch
        {
            "cactus" => CactusLongReplies,
            "sunflower" => SunflowerLongReplies,
            "mint" => MintLongReplies,
            "fern" => FernLongReplies,
            "bamboo" => BambooLongReplies,
            _ => CactusLongReplies
        };

        return reply + " " + extra[random.Next(extra.Length)];
    }

    private static readonly string[] CactusLongReplies =
    [
        "沙漠里昼夜温差大，白天晒得发烫，夜里又冷得彻骨。我能活下来，是因为懂得把水分藏起来，也懂得把情绪放在心里慢慢熬。人也是一样，慢一点，稳一点，会更久。",
        "仙人掌不用天天浇水，但每一次水分都特别珍贵。就像你遇到的好事与支持，不一定多，却能让你撑过很长一段路。把这些小小的温暖记住，它们会陪你。"
    ];

    private static readonly string[] SunflowerLongReplies =
    [
        "向日葵会追着太阳转，是因为它把方向感当成一种本能。你也可以给自己设一个小目标，不必宏大，但能指引你前进。慢慢走，阳光就会照到你身上。",
        "阳光充足时我长得更快，但阴天也不会让我停止生长。你也不需要在低潮时否定自己，情绪像天气，总会有转晴的一天。先照顾好自己，再去追光。"
    ];

    private static readonly string[] MintLongReplies =
    [
        "薄荷喜欢湿润和清爽的空气，太闷太热会让我蔫掉。人也是，空间感和边界感很重要。给自己留一点透气的地方，你会更清醒，也更有力量。",
        "我会在清晨释放香气，因为那是最容易让人清醒的时刻。你也可以给自己一个固定的仪式，比如一杯水、一次深呼吸，让大脑知道：现在要开始了。"
    ];

    private static readonly string[] FernLongReplies =
    [
        "蕨类喜欢柔和的光和稳定的湿度，它们不是一夜之间长大，而是慢慢舒展每一片叶子。你也可以允许自己慢慢变好，不必强迫自己立刻变得很厉害。",
        "森林里安静的时候，我能听见水滴落在叶片上的声音。这样的安静其实很珍贵，能让你更清楚自己真正想要什么。给自己一点安静的时间吧。"
    ];

    private static readonly string[] BambooLongReplies =
    [
        "竹子在地里扎根四年，第五年才会快速长高。很多努力都发生在别人看不见的地方，但那不代表它们没有意义。你在默默积累的一切，都会在合适的时机爆发。",
        "竹节一节一节地长，每一步都紧密相连。做事也是这样：把每一步做好，最终就会形成稳定的结构。别急，稳稳向前，风雨也拦不住你。"
    ];
}

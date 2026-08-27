using RotationDating.Web.Models;

namespace RotationDating.Web.Services;

internal static class SiteContentSeed
{
    public static readonly Dictionary<string, string> Settings = new()
    {
        [SiteContentKeys.HeroTitle] = "Green That Grows With You",
        [SiteContentKeys.HeroDesc] = "식물의 에너지로,\n당신을 새롭게 채워보세요",
        [SiteContentKeys.HeroImage] = "https://images.unsplash.com/photo-1490750967868-88aa4486c946?auto=format&fit=crop&w=1800&q=80",
        [SiteContentKeys.IntroNotice] = "수업문의는 카카오톡 '싱싱한팜' 채널을 이용해 주세요.\n수업시간에는 답변이 다소 지연될 수 있습니다.",
        [SiteContentKeys.IntroNoticeEn] = "For class inquiries, please use KakaoTalk or the homepage.\nReplies may be delayed during class hours.",
        [SiteContentKeys.KakaoUrl] = "https://pf.kakao.com/_cxcUFb",
        [SiteContentKeys.InstagramUrl] = "https://www.instagram.com/",
        [SiteContentKeys.StoreUrl] = "https://smartstore.naver.com/",
        [SiteContentKeys.AboutTitle] = "About SSF",
        [SiteContentKeys.AboutBody] = "싱싱한팜은 꽃·식물·공간 연출과 원예 교육을 중심으로,\n일상과 행사에 싱싱한 경험을 전합니다.",
        [SiteContentKeys.AboutImage] = "https://images.unsplash.com/photo-1525310072745-f49212b5ac6d?auto=format&fit=crop&w=800&q=80",
        [SiteContentKeys.AboutList] = "온라인·오프라인 꽃·식물·제로웨이스트 수업 진행\n연령·대상별 맞춤 수업 및 기업·복지 힐링 프로그램\n화훼장식기능사 · 조경기능사\n도시농업 · 힐링가든 · 치유농장 운영",
        [SiteContentKeys.EventEyebrow] = "Event by SSF",
        [SiteContentKeys.EventTitle] = "slow blossom",
        [SiteContentKeys.EventDesc] = "싱싱한팜에서 기획·운영하는 프리미엄 로테이션 소개팅 행사입니다.\n우노커피, 호텔수성스퀘어 등에서 만나보세요.",
        [SiteContentKeys.FooterCompany] = "SSF 싱싱한팜",
        [SiteContentKeys.FooterOwner] = "이효정",
        [SiteContentKeys.FooterEmail] = "singsingfarm22@naver.com",
        [SiteContentKeys.FooterBizNo] = "354-70-00725",
        [SiteContentKeys.FooterAddress] = "대구광역시 달서구 송현로 113, 2층"
    };

    public static readonly SiteSection[] Sections =
    [
        new()
        {
            Heading = "Bring Freshness Home",
            Title = "싱싱함으로 공기정화식물 선물하세요",
            Body = "고객님을 대신하여 선물을 전달한다는 마음으로\n싱싱한 식물로 보답드립니다.\n리본문구 무료, 배송비 무료 (군, 산간지역에 따라 추가금 발생가능)",
            ImageUrl1 = "https://images.unsplash.com/photo-1485955900006-10f4d324d411?auto=format&fit=crop&w=700&q=80",
            ImageUrl2 = "https://images.unsplash.com/photo-1416879595882-3373a0480b5b?auto=format&fit=crop&w=700&q=80",
            ImageAlt1 = "공기정화식물",
            ImageAlt2 = "화분 연출",
            IsReversed = false,
            SortOrder = 1
        },
        new()
        {
            Heading = "Bring the Holiday Magic to Your Space",
            Title = "크리스마스 트리 설치, 대여",
            Body = "모델하우스, 콘서트장, 박물관, 교육청, 시청, 의회, 구청, 호텔,\n병원, 대형카페, 학원, 학교 등 다양한 곳에 크리스마스 트리 설치를 합니다.\n구입 및 대여 가능합니다. 시즌 상품 미리 준비하세요!",
            ImageUrl1 = "https://images.unsplash.com/photo-1512389142860-9c449e58a543?auto=format&fit=crop&w=700&q=80",
            ImageUrl2 = "https://images.unsplash.com/photo-1543589077-47d81606c1bf?auto=format&fit=crop&w=700&q=80",
            ImageAlt1 = "크리스마스 트리 연출",
            ImageAlt2 = "홀리데이 공간 연출",
            IsReversed = true,
            SortOrder = 2
        },
        new()
        {
            Heading = "Plant styling that fits your space",
            Title = "공간에 맞는\n플랜테리어 조경으로 안내드립니다",
            Body = "현장 상담을 통해 원하시는 디자인에 대해 상의 후,\n작업이 들어가게 됩니다. 공간에 맞는 조경으로 분위기를 바꿔보세요!",
            ImageUrl1 = "https://images.unsplash.com/photo-1463320726281-696a485928c7?auto=format&fit=crop&w=700&q=80",
            ImageUrl2 = "https://images.unsplash.com/photo-1459156212016-c812468e2115?auto=format&fit=crop&w=700&q=80",
            ImageAlt1 = "플랜테리어 공간",
            ImageAlt2 = "공간 조경 연출",
            IsReversed = false,
            SortOrder = 3
        }
    ];

    public static readonly SiteFeatureCard[] FeatureCards =
    [
        new()
        {
            Eyebrow = "싱싱한팜 네이버스마트스토어",
            Title = "Farm Store",
            Pill = "Smart Store",
            ImageUrl = "https://images.unsplash.com/photo-1462275646964-a0e3386b89fa?auto=format&fit=crop&w=900&q=80",
            LinkUrl = "https://smartstore.naver.com/",
            Variant = "store",
            SortOrder = 1
        },
        new()
        {
            Eyebrow = "International Flower Stylist Association",
            Title = "Class & Certification",
            Pill = "Applying for a class",
            ImageUrl = "https://images.unsplash.com/photo-1487530811176-3780de880c2d?auto=format&fit=crop&w=900&q=80",
            LinkUrl = "https://pf.kakao.com/_cxcUFb",
            Variant = "class",
            SortOrder = 2
        }
    ];

    public static readonly SiteGalleryItem[] GalleryItems =
    [
        new() { Category = "크리스마스 트리", Caption = "크리스마스 트리 영천여고 3M 트리", ImageUrl = "https://images.unsplash.com/photo-1512389142860-9c449e58a543?auto=format&fit=crop&w=800&q=80", SortOrder = 1 },
        new() { Category = "크리스마스 트리", Caption = "로비 트리 연출", ImageUrl = "https://images.unsplash.com/photo-1543589077-47d81606c1bf?auto=format&fit=crop&w=800&q=80", SortOrder = 2 },
        new() { Category = "개업화분", Caption = "개업·축하 화분 연출", ImageUrl = "https://images.unsplash.com/photo-1487530811176-3780de880c2d?auto=format&fit=crop&w=800&q=80", SortOrder = 3 },
        new() { Category = "원예 프로그램", Caption = "원예 클래스 현장", ImageUrl = "https://images.unsplash.com/photo-1416879595882-3373a0480b5b?auto=format&fit=crop&w=800&q=80", SortOrder = 4 },
        new() { Category = "행사", Caption = "slow blossom 로테이션 소개팅", ImageUrl = "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=800&q=80", SortOrder = 5 },
        new() { Category = "조경", Caption = "공간 플랜테리어 조경", ImageUrl = "https://images.unsplash.com/photo-1459156212016-c812468e2115?auto=format&fit=crop&w=800&q=80", SortOrder = 6 }
    ];

    public static readonly SiteFaqItem[] FaqItems =
    [
        new()
        {
            Question = "크리스마스트리 대여 문의는 언제부터 가능한가요?",
            Answer = "크리스마스트리는 시즌 상품이기에 빠르게 설치하실수록 좋습니다.\n기업에서는 7-8월부터 미팅을 하셔서 10-11월에 설치를 하는 것을 추천드립니다.",
            SortOrder = 1
        },
        new()
        {
            Question = "수업 공간은 어떻게 되나요?",
            Answer = "모두 예약제로 진행이 되기 때문에 당일 사용은 어렵습니다.\n1. 중구 소재 : 대구 중구 태평로 160, 10층\n2. 수성구 소재 : 대구 수성구 달구벌대로 528길 15, 수성대학교 성요셉관 3층\n3. 달서구 소재 : 대구 달서구 중흥로 4길, 3층",
            SortOrder = 2
        },
        new()
        {
            Question = "출강 문의 드립니다. 예산에 맞춰 준비 가능할까요?",
            Answer = "강사비가 없는 수업은 출강을 하지 않습니다.\n강사비와 재료비가 있는 수업만을 진행하며, 예산 내에 원예수업 가능한 목록으로 안내드립니다.",
            SortOrder = 3
        },
        new()
        {
            Question = "조경 제안을 받고 싶어요. 현장에도 와주시나요?",
            Answer = "조경은 현장 미팅 후, 견적을 보내드립니다.\n현장 미팅을 갈 때에 대표님이 원하시는 디자인이 있으시면 먼저 사진을 보여주시면 빠른 상담이 가능하며, 현장 미팅 상담비용은 발생합니다.",
            SortOrder = 4
        }
    ];
}

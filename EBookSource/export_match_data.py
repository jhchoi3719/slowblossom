# 궁합 테스트(/love/match)용 데이터 내보내기: python -I export_match_data.py
# 전자책 원고(combos.py, types_data.py)를 wwwroot/mbti-match-data.js 로 옮긴다
import json, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from combos import COMBOS
from types_data import GROUPS, MAKEUP, TYPES

# NT+NT는 원본 PDF에만 있으므로 원문에서 옮겨 둔다
NT_NT = {
    "key": "NT+NT",
    "title": "NT + NT · 생각은 빠르게 통하고, 마음은 늦게 도착하는 두 사람",
    "intro": "같은 그룹이라 대화의 속도와 방식이 비슷해서, 첫 30분은 연애 중에서도 손에 꼽히게 편합니다. 대신 둘 다 감정 표현을 뒤로 미루는 사람들이라, 썸이 \"친한 토론 상대\"에서 멈추기 쉬운 조합이기도 해요.",
    "glance": [("끌리는 이유", "생각의 속도가 같고, 설명하지 않아도 맥락을 알아듣는 편안함"),
               ("가장 큰 함정", "서로의 침묵을 \"관심 없음\"으로 읽고 먼저 물러나는 것")],
    "first_week": ["\"하루에 한 번은 한 줄이라도 보내기\"처럼 느슨한 연락 규칙을 정하기",
                   "대화가 끝날 때 \"오늘 기분 어땠어요?\"를 한 번 묻기"],
    "oneline": "말이 잘 통해서 마음도 통했을 거라고 생각하기 쉽습니다. 하지만 마음은 말로 한 번 더 확인해야 도착합니다.",
    "weeks": [("3주차", "대화는 여전히 좋지만 \"우리 뭐지?\"라는 생각이 시작됨", "한쪽이 먼저 솔직해지기. 약간의 타이밍 태우기")],
    "confess_lines": [("조용한 카페에서 만났을 때", "\"만날수록 이야기가 더 하고 싶어졌어요. 이제는 친구 이상으로 만나고 싶은데, 어떠세요?\"")],
    "hurt": [("내 한 말에 \"그건 근거가 약해\"라는 반론이 돌아옴", "토론인지 공격인지 헷갈림",
              "\"토론이 재밌다는 뜻인데, 지금 조금 방어적이 됐어요. 한 번만 더 들어 주실래요?\"")],
}


def combo(c):
    g = dict(c["glance"])
    week3 = next(w for w in c["weeks"] if w[0] == "3주차")
    return {
        "title": c["title"].split(" · ", 1)[1],
        "intro": c["intro"],
        "pull": g["끌리는 이유"],
        "trap": g["가장 큰 함정"],
        "efforts": c["first_week"],
        "oneline": c["oneline"],
        "week3": {"what": week3[1], "tip": week3[2]},
        "confess": [{"who": w, "line": l} for w, l in c["confess_lines"]],
        "hurt": {"scene": c["hurt"][0][0], "say": c["hurt"][0][2]},
    }


data = {
    "types": {code: {"group": t[0], "nick": t[1], "style": t[2], "love": t[3], "approach": t[5]} for code, t in TYPES.items()},
    "groups": {k: {"goodQ": g["good_q"], "avoidQ": g["avoid_q"], "place": g["place"]} for k, g in GROUPS.items()},
    "makeup": MAKEUP,
    "combos": {c["key"]: combo(c) for c in [NT_NT, *COMBOS]},
}
out = HERE.parent / "wwwroot" / "mbti-match-data.js"
out.write_text("// EBookSource/export_match_data.py 로 생성됨. 직접 고치지 말 것\nwindow.MBTI_MATCH = "
               + json.dumps(data, ensure_ascii=False, indent=1) + ";\n", encoding="utf-8")
print("wrote", out, len(data["types"]), "types", len(data["combos"]), "combos")

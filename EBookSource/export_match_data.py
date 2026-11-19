# 궁합 테스트(/love/match)용 데이터 내보내기: python -I export_match_data.py
# 전자책 원고(combos.py, types_data.py)를 wwwroot/mbti-match-data.js 로 옮긴다
import json, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from combos import COMBOS
from types_data import TYPES

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
}


def combo(c):
    g = dict(c["glance"])
    return {
        "title": c["title"].split(" · ", 1)[1],
        "intro": c["intro"],
        "pull": g["끌리는 이유"],
        "trap": g["가장 큰 함정"],
        "efforts": c["first_week"],
        "oneline": c["oneline"],
    }


data = {
    "types": {code: {"group": t[0], "nick": t[1], "style": t[2], "love": t[3], "approach": t[5]} for code, t in TYPES.items()},
    "combos": {c["key"]: combo(c) for c in [NT_NT, *COMBOS]},
}
out = HERE.parent / "wwwroot" / "mbti-match-data.js"
out.write_text("// EBookSource/export_match_data.py 로 생성됨. 직접 고치지 말 것\nwindow.MBTI_MATCH = "
               + json.dumps(data, ensure_ascii=False, indent=1) + ";\n", encoding="utf-8")
print("wrote", out, len(data["types"]), "types", len(data["combos"]), "combos")

# 전자책 빌드: python -I build.py
# 결과물은 out/ 에 생성 (EBookSeed 원본은 건드리지 않음)
import html, subprocess, sys
from pathlib import Path
import pymupdf

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from combos import COMBOS
from types_data import GROUPS, TYPES, MAKEUP

SEED = HERE.parent / "EBookSeed"
OUT = HERE / "out"
TMP = OUT / "_tmp"
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
SITE = "https://slowblossom.onrender.com"
LOVE_URL = f"{SITE}/love"
_FONTS = [Path(r"C:\Windows\Fonts"), Path.home() / "AppData/Local/Microsoft/Windows/Fonts"]
FONT_SANS = str(next(d / "NotoSansKR-Regular.ttf" for d in _FONTS if (d / "NotoSansKR-Regular.ttf").exists()))
FONT_LIGHT = str(next(d / "NotoSansKR-Light.ttf" for d in _FONTS if (d / "NotoSansKR-Light.ttf").exists()))
e = html.escape


def render(name, body, css):
    src = TMP / f"{name}.html"
    pdf = TMP / f"{name}.pdf"
    src.write_text(f"<!doctype html><html lang='ko'><head><meta charset='utf-8'><style>{css}</style></head><body>{body}</body></html>", encoding="utf-8")
    subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--no-pdf-header-footer",
                    f"--print-to-pdf={pdf}", src.as_uri()], check=True, capture_output=True, timeout=120)
    return pdf


# ---------- MBTI 가이드 스타일 (원본: A4, 본문 12pt, 제목 세리프) ----------
MBTI_CSS = """
@page { size: 595.92pt 842.88pt; margin: 48pt 46pt 44pt 46pt; }
body { font-family: 'Noto Sans KR', 'Malgun Gothic', sans-serif; font-size: 11.5pt; line-height: 1.75; color: #1f1e1d; margin: 0; }
h1 { font-family: 'Noto Serif KR', serif; font-weight: 500; font-size: 19pt; line-height: 1.4; margin: 0 0 10pt; }
h2 { font-family: 'Noto Serif KR', serif; font-weight: 500; font-size: 14pt; margin: 20pt 0 6pt; }
h3 { font-size: 11.5pt; font-weight: 700; margin: 14pt 0 4pt; }
p { margin: 0 0 8pt; }
.chapter { page-break-before: always; }
.chapter:first-child { page-break-before: auto; }
table { width: 100%; border-collapse: collapse; margin: 6pt 0 12pt; font-size: 10.5pt; line-height: 1.55; page-break-inside: avoid; }
th, td { border: 1px solid #dedcd8; padding: 6pt 7pt; text-align: left; vertical-align: top; }
th { background: #f2f1ef; font-weight: 500; }
h2, h3 { page-break-after: avoid; break-after: avoid; }
tr, li, .quote { page-break-inside: avoid; break-inside: avoid; }
.quote { border-left: 3px solid #dedcd8; padding: 2pt 0 2pt 14pt; margin: 8pt 0 10pt; }
.quote p { margin: 0 0 5pt; }
ul, ol { margin: 0 0 8pt; padding-left: 18pt; } li { margin-bottom: 3pt; }
.box { margin: 4pt 0; } .check li { list-style: none; } .check li::before { content: '☐ '; }
a { color: #c9677f; font-weight: 700; text-decoration: none; }
"""


def combo_html(c):
    h = [f"<section class='chapter'><h1>{e(c['title'])}</h1><p>{e(c['intro'])}</p>"]
    h.append("<h2>이 조합 한눈에 보기</h2><table><tr><th style='width:26%'>항목</th><th>내용</th></tr>")
    h += [f"<tr><td>{e(k)}</td><td>{e(v)}</td></tr>" for k, v in c["glance"]]
    h.append("</table><h2>1. 왜 서로에게 끌릴까</h2>")
    h += [f"<p>{e(p)}</p>" for p in c["why"]]
    h.append(f"<p>{e(c['shadow'])}</p><h2>2. 소개팅 첫 만남: 로테이션 5분 안에 일어나는 일</h2>")
    h += [f"<p><b>{e(k)}.</b> {e(v)}</p>" for k, v in c["rotation"]]
    h.append("<p>실제 장면을 예시로 보겠습니다.</p><div class='quote'>")
    h += [f"<p>{e(who)}: {e(line)}</p>" for who, line in c["dialogue"]]
    h.append(f"</div><p>{e(c['dialogue_note'])}</p><h2>3. 첫 만남 뒤 연락: 답장 속도를 어떻게 읽을까</h2><p>{e(c['contact'])}</p><h3>연락에서 쓸 수 있는 짧은 한 줄</h3><ul>")
    h += [f"<li><b>{e(k)}:</b> {e(v)}</li>" for k, v in c["contact_lines"]]
    h.append("</ul><h2>4. 썸 단계: 주 단위로 보는 흐름</h2><table><tr><th style='width:16%'>시기</th><th>일어나는 일</th><th>이때 주의할 점</th></tr>")
    h += [f"<tr><td>{e(a)}</td><td>{e(b)}</td><td>{e(d)}</td></tr>" for a, b, d in c["weeks"]]
    h.append("</table><h2>5. 서운함이 생기는 순간과 말 바꾸기</h2><table><tr><th>장면</th><th style='width:24%'>속마음</th><th>이렇게 말해 보세요</th></tr>")
    h += [f"<tr><td>{e(a)}</td><td>{e(b)}</td><td>{e(d)}</td></tr>" for a, b, d in c["hurt"]]
    h.append(f"</table><h2>6. 고백: 누가, 언제, 어떤 말로</h2><p>{e(c['confess_types'])}</p><table><tr><th style='width:24%'>상황</th><th>문장 예시</th></tr>")
    h += [f"<tr><td>{e(a)}</td><td>{e(b)}</td></tr>" for a, b in c["confess_lines"]]
    h.append("</table><h2>7. 고백 이후 첫 일주일</h2><ol>")
    h += [f"<li>{e(x)}</li>" for x in c["first_week"]]
    cs = c["case"]
    h.append(f"</ol><h2>8. 현실 사례: 이런 일, 한 번쯤 있었죠</h2><p style='font-size:10pt;color:#6b6b6b'>실제 인물이 아닌 가상 사례이며, 이름은 모두 가명입니다.</p>"
             f"<h3>{e(cs['title'])}</h3><p>{e(cs['story'])}</p><ul>"
             f"<li><b>무슨 일이었나:</b> {e(cs['what'])}</li><li><b>속마음:</b> {e(cs['inner'])}</li><li><b>이렇게 바꿔 보면:</b> {e(cs['fix'])}</li></ul>"
             "<h2>9. 체크리스트와 한 줄 정리</h2><ul class='check'>")
    h += [f"<li>{e(x)}</li>" for x in c["checklist"]]
    h.append(f"</ul><p><b>이 장의 한 줄 정리.</b> {e(c['oneline'])}</p></section>")
    return "".join(h)


ENDING_HTML = f"""<section class='chapter'><h1>마치며</h1>
<p>MBTI는 상대를 이해하는 첫 문일 뿐이고, 그 뒤는 직접 만나고 대화하며 채워집니다. 슬로우블러썸은 그 대화가 자연스럽게 시작되는 자리를 만듭니다.</p>
<h3>슬로우블러썸을 더 만나는 법</h3><ul>
<li><b>로테이션 소개팅 (대구·경북):</b> 토요일마다 남 5·여 5 신청이 차면 열려요. 신청: <a href='{SITE}/apply/8'>신청서 바로 열기</a></li>
<li><b>카카오톡 채널:</b> 행사 일정과 상황별 연애 팁을 가장 먼저 받아 보세요. <a href='https://pf.kakao.com/_cxcUFb'>채널 추가하기</a></li>
<li><b>인스타그램:</b> MBTI 연애 카드뉴스를 올려요. <a href='https://www.instagram.com/slowblossom.daegu'>@slowblossom.daegu</a></li></ul>
<h3>다음 책을 위한 1분 설문</h3>
<p>연애 전·연애 중·결혼 후는 고민이 전혀 다릅니다. 지금 상황을 알려주시면, 당신의 상황에 맞는 다음 콘텐츠와 연애 상담 서비스를 만드는 데 쓰겠습니다. <a href='{LOVE_URL}'>설문하러 가기</a></p>
<ol><li>지금 나는: 연애 전 / 연애 중 / 결혼 후</li><li>(결혼 후라면) 자녀 유무, 맞벌이 여부</li><li>나의 MBTI와 상대(또는 관심 있는 사람)의 MBTI</li><li>이 책에서 가장 도움이 된 부분과 더 알고 싶은 주제</li></ol>
<p>응답 내용은 개인을 알아볼 수 없는 형태로 모아 서비스 개선에만 사용합니다.</p>
<p>슬로우블러썸 · 운영 싱싱한팜 · 대표 이효정</p>
<p style='font-size:10pt;color:#6b6b6b'>MBTI는 The Myers-Briggs Company의 상표이며, 이 책은 해당 회사와 관련이 없는 독자적인 연애 가이드입니다.</p></section>"""


def stamp_mbti(doc):
    """모든 페이지의 헤더·쪽번호를 새로 찍는다 (원본 'Page x of 15' 교체)."""
    n = len(doc)
    for i, page in enumerate(doc):
        w, hgt = page.rect.width, page.rect.height
        page.add_redact_annot(pymupdf.Rect(0, hgt - 30, w, hgt), fill=None)
        page.apply_redactions(images=pymupdf.PDF_REDACT_IMAGE_NONE, graphics=pymupdf.PDF_REDACT_LINE_ART_NONE)
        page.insert_font(fontname="nk", fontfile=FONT_SANS)
        label = f"Page {i + 1} of {n}"
        page.insert_text((w - 46 - pymupdf.get_text_length(label, fontsize=6), hgt - 18), label, fontsize=6, color=(0.42, 0.42, 0.42))
        if 15 <= i < n - 1:  # 새로 만든 페이지에도 원본과 같은 머리말 (마지막 링크 페이지 제외)
            page.insert_text((46, 22), "슬로우블러썸 MBTI 연애 가이드", fontname="nk", fontsize=6, color=(0.42, 0.42, 0.42))


def build_mbti_full():
    orig = pymupdf.open(SEED / "mbti-love-guide.pdf")
    full = pymupdf.open()
    full.insert_pdf(orig, from_page=0, to_page=14)
    # 15쪽의 '마치며'(y≈130 아래)는 조합편 뒤로 옮기므로 지운다
    p15 = full[14]
    for lk in p15.get_links():
        if lk["from"].y0 > 125:
            p15.delete_link(lk)
    p15.add_redact_annot(pymupdf.Rect(0, 125, p15.rect.width, p15.rect.height - 30), fill=(1, 1, 1))
    p15.apply_redactions(images=pymupdf.PDF_REDACT_IMAGE_REMOVE)
    body = "".join(combo_html(c) for c in COMBOS) + ENDING_HTML
    full.insert_pdf(pymupdf.open(render("combos", body, MBTI_CSS)))
    full.insert_pdf(orig, from_page=15, to_page=len(orig) - 1)  # 원본 마지막 링크 페이지
    stamp_mbti(full)
    full.save(OUT / "mbti-love-guide-full.pdf", garbage=3, deflate=True)
    return len(full)


# ---------- 16유형 맛보기 (한 장) ----------
SAMPLE_CSS = """
@page { size: 595.92pt 842.88pt; margin: 0; }
body { margin: 0; font-family: 'Noto Sans KR', 'Malgun Gothic', sans-serif; color: #1f1e1d; }
.page { height: 842pt; box-sizing: border-box; padding: 40pt 46pt 30pt; background: #faf7f2; position: relative; }
.brand { font-size: 8pt; letter-spacing: 2pt; color: #c9677f; }
.code { font-family: 'Noto Serif KR', serif; font-size: 46pt; font-weight: 700; color: #1f4d3a; margin: 10pt 0 0; line-height: 1.1; }
.nick { font-family: 'Noto Serif KR', serif; font-size: 17pt; margin: 4pt 0 2pt; }
.group { font-size: 9.5pt; color: #6b6b6b; margin-bottom: 12pt; }
.desc { font-size: 10pt; line-height: 1.7; background: #fff; border-radius: 8pt; padding: 9pt 12pt; margin-bottom: 12pt; }
.item { margin: 0 0 9pt; font-size: 11pt; line-height: 1.65; } .item b { color: #1f4d3a; display: block; font-size: 10pt; }
.two { display: flex; gap: 10pt; margin-top: 4pt; } .two > div { flex: 1; background: #fff; border-radius: 8pt; padding: 9pt 11pt; font-size: 9.5pt; line-height: 1.6; }
.two h4 { margin: 0 0 4pt; font-size: 10pt; color: #c9677f; }
.cta { position: absolute; left: 46pt; right: 46pt; bottom: 34pt; background: #1f4d3a; color: #fff; border-radius: 10pt; padding: 13pt 16pt; font-size: 10pt; line-height: 1.6; }
.cta b { font-size: 12.5pt; } .cta a { color: #ffd6df; font-weight: 700; text-decoration: none; }
.note { font-size: 7pt; color: #9a9a9a; position: absolute; left: 46pt; bottom: 16pt; }
"""


def sample_html(code):
    g, nick, style, love, hurt, approach = TYPES[code]
    gr = GROUPS[g]
    mk = "<br>".join(f"<b style='color:#1f4d3a'>{k}</b> {e(MAKEUP[k])}" for k in (code[0], code[2]))
    qs = "<br>".join(f"“{e(q)}”" for q in gr["good_q"])
    return f"""<div class='page'><div class='brand'>SLOW BLOSSOM · MBTI 연애 가이드 맛보기</div>
<div class='code'>{code}</div><div class='nick'>{e(nick)}</div><div class='group'>{e(gr['name'])}</div>
<div class='desc'>{e(gr['desc'])}</div>
<p class='item'><b>연애 스타일</b>{e(style)}</p><p class='item'><b>애정 표현</b>{e(love)}</p>
<p class='item'><b>서운한 순간</b>{e(hurt)}</p><p class='item'><b>이렇게 다가가세요</b>{e(approach)}</p>
<div class='two'><div><h4>첫 만남, 이 질문부터</h4>{qs}<br><span style='color:#6b6b6b'>피하면 좋은 질문: {e(gr['avoid_q'])}<br>좋은 첫 데이트 장소: {e(gr['place'])}</span></div>
<div><h4>다툰 뒤 화해할 때</h4>{mk}</div></div>
<div class='cta'><b>나와 그 사람, 어디서 끌리고 어디서 부딪힐까?</b><br>
16유형 전체 + 10가지 조합편(썸·첫 만남·고백 문장까지)이 담긴 전체 가이드를 <b>550원</b>에 볼 수 있어요.<br>
<a href='{LOVE_URL}'>전체 가이드 보러 가기 →</a> &nbsp; <a href='{LOVE_URL}'>1분 설문 남기기 →</a></div>
<div class='note'>MBTI는 The Myers-Briggs Company의 상표이며, 이 자료는 해당 회사와 관련이 없는 독자적인 연애 가이드입니다.</div></div>"""


def build_samples():
    d = OUT / "samples"
    d.mkdir(exist_ok=True)
    for code in TYPES:
        pdf = render(f"sample_{code}", sample_html(code), SAMPLE_CSS)
        doc = pymupdf.open(pdf)
        doc.save(d / f"mbti-sample-{code}.pdf", garbage=3, deflate=True)


# ---------- 연애 시작 가이드: 부록 E 'MBTI로 다시 읽기' ----------
LOVE_CSS = """
@page { size: 600pt 900pt; margin: 0; }
body { margin: 0; font-family: 'Noto Sans KR', 'Malgun Gothic', sans-serif; color: #2b332e; }
.pg { height: 900pt; box-sizing: border-box; padding: 52pt 48pt 40pt; background: #faf7f2; position: relative; page-break-after: always; }
.kicker { color: #e58ba1; letter-spacing: 2pt; font-size: 10pt; }
h1 { font-family: 'Gowun Batang', 'Noto Serif KR', serif; color: #1f4d3a; font-size: 26pt; margin: 6pt 0 4pt; }
.bar { width: 60pt; height: 3pt; background: #e58ba1; margin: 6pt 0 14pt; }
p { font-size: 11.5pt; line-height: 1.8; margin: 0 0 10pt; }
table { width: 100%; border-collapse: collapse; font-size: 10.5pt; line-height: 1.6; }
th { background: #1f4d3a; color: #fff; font-weight: 500; padding: 7pt; text-align: left; }
td { border-bottom: 1px solid #dfe3dc; padding: 8pt 7pt; vertical-align: top; } td:first-child { color: #1f4d3a; font-weight: 700; width: 24%; }
.tip { background: #f6d9e0; border-left: 4px solid #e58ba1; padding: 10pt 14pt; font-size: 10.5pt; line-height: 1.7; margin-top: 14pt; }
.tip a { color: #1f4d3a; font-weight: 700; }
.num { position: absolute; bottom: 30pt; left: 0; right: 0; text-align: center; color: #7a867f; font-size: 11pt; }
"""

LOVE_ROWS_1 = [
    ("1장 나는 어떤 연애를 원하는가", "E는 '밖에서 함께', I는 '둘만 조용히' 쉬는 주말을 원하기 쉬워요.", "기본 조건에 '주말을 보내는 방식'을 한 줄 넣어 보세요."),
    ("3장 첫인상보다 중요한 첫 대화", "S는 구체적인 질문(오늘 뭐 했어요?), N은 의미를 묻는 질문(왜 그 일을 해요?)에 신이 나요.", "반응이 좋은 질문 쪽으로 대화를 이어 가세요."),
    ("4장 연락의 리듬 맞추기", "I는 답이 느려도 관심이 식은 게 아니라 '충전 중'이에요. J는 정해진 시간에, P는 생각날 때 연락해요.", "\"저는 답이 느린 편이에요\"처럼 내 리듬을 먼저 알려 주세요."),
    ("5장 두 번째 약속 잡는 법", "J에게는 날짜·장소를 구체적으로, P에게는 선택지와 여유를 주면 편해요.", "\"토요일 2시 어때요?\"(J) / \"이번 주 중 편한 날 있어요?\"(P)"),
]
LOVE_ROWS_2 = [
    ("6장 호감 신호 읽기", "NT는 질문이 많아지고, NF는 내 말을 기억하고, SJ는 약속을 꼬박꼬박 지키고, SP는 '같이 하자'고 제안해요.", "상대 유형의 방식으로 나타나는 신호를 찾아보세요."),
    ("7장 고백 전에 할 일", "T에게는 '이유 + 감정 한 줄', F에게는 '감정 먼저'가 잘 전해져요.", "\"같이 있으면 편해서(이유) 좋아해요(감정).\""),
    ("9장 첫 갈등을 대하는 법", "I는 정리할 시간이, E는 바로 푸는 대화가 필요해요. T는 '다음엔 어떻게', F는 '서운했겠다'가 먼저예요.", "화해의 순서를 상대 유형에 맞춰 보세요."),
    ("10장 가치관을 맞춰보는 대화", "J는 계획 질문(5년 뒤 어디 살고 싶어요?), P는 경험 질문(어떤 여행이 좋았어요?)으로 열면 편해요.", "무거운 주제도 상대가 편한 문으로 들어가세요."),
    ("11장 바쁜 직장인의 시간 관리", "J는 고정 데이트(매주 토요일)가, P는 유연한 약속(이번 주 중 하루)이 편해요.", "둘이 섞이면 '고정 1회 + 자유 1회'로 나눠 보세요."),
]


def love_table(rows):
    return "<table><tr><th>이 책의 장</th><th>MBTI로 보면</th><th>해볼 한 가지</th></tr>" + "".join(
        f"<tr><td>{e(a)}</td><td>{e(b)}</td><td>{e(c)}</td></tr>" for a, b, c in rows) + "</table>"


def build_love(start_num):
    body = f"""<div class='pg'><div class='kicker'>APPENDIX E</div><h1>MBTI로 다시 읽는 연애 시작</h1><div class='bar'></div>
<p>같은 조언도 사람마다 다르게 닿습니다. 이 책의 각 장을 MBTI 네 글자로 한 번 더 읽어 보세요. 나와 상대의 유형을 안다면, '오늘 해볼 한 가지'를 더 정확하게 고를 수 있어요.</p>
{love_table(LOVE_ROWS_1)}<div class='num'>{start_num}</div></div>
<div class='pg'><div class='kicker'>APPENDIX E</div><h1>MBTI로 다시 읽는 연애 시작</h1><div class='bar'></div>
{love_table(LOVE_ROWS_2)}
<div class='tip'><b>MBTI는 정답이 아니라 대화의 출발점이에요.</b> 유형으로 사람을 단정하지 말고, "혹시 이럴 때 이런 편이에요?"라고 물어보는 데 써 주세요.<br>
16유형과 10가지 조합을 자세히 보고 싶다면 → <a href='{LOVE_URL}'>슬로우블러썸 MBTI 연애 가이드</a></div>
<div class='num'>{start_num + 1}</div></div>"""
    body = body.replace("</div>\n<div class='pg'>", "</div><div class='pg'>")
    return render("love_appendix", "<link href='https://fonts.googleapis.com/css2?family=Gowun+Batang:wght@700&display=block' rel='stylesheet'>" + body, LOVE_CSS)


def build_love_full():
    orig = pymupdf.open(SEED / "love-start-guide.pdf")
    links_idx = 23  # 24쪽: 링크 모음 페이지 (부록 D는 23쪽)
    out = pymupdf.open()
    out.insert_pdf(orig, from_page=0, to_page=links_idx - 1)
    out.insert_pdf(pymupdf.open(build_love(links_idx + 1)))
    out.insert_pdf(orig, from_page=links_idx, to_page=len(orig) - 1)
    toc = out[2]  # 목차에 부록 E 추가
    toc.insert_font(fontname="nl", fontfile=FONT_LIGHT)
    toc.insert_text((92, 757), "E. MBTI로 다시 읽는 연애 시작", fontname="nl", fontsize=15.4, color=(0x2b / 255, 0x33 / 255, 0x2e / 255))
    toc.insert_text((535, 757), str(links_idx + 1), fontname="nl", fontsize=15.8, color=(0x7a / 255, 0x86 / 255, 0x7f / 255))
    out.save(OUT / "love-start-guide-mbti.pdf", garbage=3, deflate=True)
    return len(out)


if __name__ == "__main__":
    TMP.mkdir(parents=True, exist_ok=True)
    print("mbti full pages:", build_mbti_full())
    build_samples()
    print("samples:", len(list((OUT / "samples").glob("*.pdf"))))
    print("love pages:", build_love_full())

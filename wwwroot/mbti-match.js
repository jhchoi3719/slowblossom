// MBTI 궁합 테스트 (/love/match). 입력한 이름은 서버로 보내지 않고 이 화면 안에서만 쓴다.
(function () {
    const D = window.MBTI_MATCH;
    const form = document.getElementById("match-form");
    const out = document.getElementById("match-result");
    const loading = document.getElementById("match-loading");
    if (!D || !form || !out || !loading) return;

    const GROUP_ORDER = ["NT", "NF", "SJ", "SP"];
    // 궁합 점수 규칙 (EBookSource/plans/01-진단과-기획.md 프롬프트 3). 같은 MBTI·단계면 항상 같은 점수
    const AXIS = { EI: 0, SN: 1, TF: 2, JP: 3 };
    const WEIGHT = { EI: 15, SN: 25, TF: 20, JP: 20 };
    const AXIS_SAME = { EI: 1.0, SN: 1.0, TF: 0.8, JP: 0.8 };
    const AXIS_DIFF = { EI: 0.7, SN: 0.5, TF: 0.9, JP: 0.75 };
    // 아직 모르는 사이·썸은 첫 대화와 리듬, 연애 중은 기본 가중치
    const STAGE_WEIGHT = {
        before: { EI: 1.3, SN: 1.4, TF: 0.8, JP: 0.6 },
        some: { EI: 1.3, SN: 1.4, TF: 0.8, JP: 0.6 },
        dating: { EI: 1, SN: 1, TF: 1, JP: 1 }
    };
    // 하위 항목: [축, 비중, 다를 때 값]
    const SUBS = [
        { label: "대화", parts: [["SN", 0.55, 0.55], ["EI", 0.25, 0.7], ["TF", 0.2, 0.75]] },
        { label: "갈등 해결", parts: [["TF", 0.45, 0.6], ["EI", 0.35, 0.7], ["JP", 0.2, 0.75]] },
        { label: "일상 리듬", parts: [["JP", 0.5, 0.55], ["EI", 0.35, 0.65], ["SN", 0.15, 0.8]] }
    ];
    const EFFORT_POINT = 70;
    // 한 글자만 다를 때 부딪히는 지점과 해법 (전자책 '한 글자만 다르면 어디서 부딪힐까')
    const AXES = [
        { i: 2, a: "T", b: "F", title: "다툰 직후, 먼저 한 가지만 물어보기",
          text: (a, b) => `${a}님은 해결책으로, ${b}님은 공감으로 마음을 표현해요. 서운한 일이 생기면 "지금 해결책이 필요해, 아니면 들어주길 바라?"라고 먼저 물어보세요. 이 한마디로 다툼의 절반은 풀려요.` },
        { i: 3, a: "J", b: "P", title: "큰 틀과 현장의 즐거움 나눠 맡기",
          text: (a, b) => `${a}님은 미리 정한 계획에서, ${b}님은 그날의 여유에서 즐거움을 느껴요. 여행과 기념일은 날짜·예산은 ${a}님이, 그날의 코스는 ${b}님이 맡아 보세요.` },
        { i: 0, a: "E", b: "I", title: "'나가는 주말'과 '쉬는 주말' 나누기",
          text: (a, b) => `${a}님은 함께 밖에서 보낼수록, ${b}님은 혼자만의 시간으로 충전돼요. 한 달에 "밖에 나가는 주말"과 "집에서 쉬는 주말"을 미리 나눠 두면 편해요.` },
        { i: 1, a: "S", b: "N", title: "대화에 한 마디씩 더 붙이기",
          text: (a, b) => `${a}님은 구체적인 이야기에, ${b}님은 의미와 상상에 설레요. ${a}님은 이야기에 '왜 좋았는지'를, ${b}님은 구체적인 예시를 한 마디씩 더 붙여 보세요.` }
    ];
    const STAGE_LABEL = { before: "아직 잘 모르는 사이", some: "썸 타는 중", dating: "연애 중" };

    const esc = s => String(s).replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

    // 마지막 글자의 받침: 0 없음, 8 ㄹ받침, 그 밖은 다른 받침
    function jong(word) {
        const code = word.charCodeAt(word.length - 1) - 0xac00;
        return code < 0 || code > 11171 ? 0 : code % 28;
    }
    const eunNeun = w => (jong(w) ? "은" : "는");
    const iGa = w => (jong(w) ? "이" : "가");
    const euro = w => (jong(w) === 0 || jong(w) === 8 ? "로" : "으로");

    function scores(a, b, stage) {
        const same = k => a[AXIS[k]] === b[AXIS[k]];
        const mul = STAGE_WEIGHT[stage];
        const raw = Object.fromEntries(Object.keys(WEIGHT).map(k => [k, WEIGHT[k] * mul[k]]));
        const sum = Object.values(raw).reduce((x, y) => x + y, 0);
        const total = Math.round(20 + Object.keys(raw).reduce((acc, k) =>
            acc + raw[k] * 80 / sum * (same(k) ? AXIS_SAME[k] : AXIS_DIFF[k]), 0));
        const sub = SUBS.map(s => {
            const v = s.parts.reduce((acc, [k, w, d]) => acc + w * (same(k) ? 1 : d), 0);
            const min = s.parts.reduce((acc, [, w, d]) => acc + w * d, 0);
            return Math.round(62 + 36 * (v - min) / (1 - min));
        });
        return { total, sub };
    }

    // 점수대별 설명 (프롬프트 3 ④). 낮은 점수는 '노력 포인트'로 부른다
    function scoreSummary(r) {
        const m = esc(r.me), y = esc(r.you);
        const order = r.sc.sub.map((v, i) => [v, SUBS[i].label]).sort((x, z) => x[0] - z[0]);
        const lowest = order[0][1];
        const points = order.filter(([v]) => v < EFFORT_POINT).map(([, l]) => l);
        const t = r.sc.total;
        if (t >= 90) return `${m}님과 ${y}님은 설명하지 않아도 통하는 부분이 많은 두 사람이에요. ` +
            (points.length ? `다만 ${points.join(", ")}${eunNeun(points[points.length - 1])} 노력 포인트예요.` : "너무 편해서 마음 표현을 미루지 않는 게 포인트예요.");
        if (t >= 85) return `잘 맞는 부분과 맞춰 갈 부분이 균형 있는 두 사람이에요. ${points[0] || lowest}만 챙기면 더 단단해져요.`;
        if (t >= 80) return `서로 다른 점에서 끌리는 두 사람이에요. ${lowest}에서 작은 약속 하나가 큰 차이를 만들어요.`;
        return `네 글자가 많이 다른 만큼 배울 게 많은 두 사람이에요. 점수보다 '어디서 맞춰 갈지'가 더 중요해요. 노력 포인트: ${(points.length ? points : [lowest]).slice(0, 2).join(", ")}`;
    }

    // 전자책 문장의 "NT는", "NF가" 같은 그룹 이름을 두 사람의 이름으로 바꾼다
    function personalize(text, names) {
        return text
            .replace(/^(NT|NF|SJ|SP) \+ (NT|NF|SJ|SP)는 /, "두 분은 ")
            .replace(/(?<![A-Z])(NT|NF|SJ|SP)(?! \+)(에게는|에게|는|은|가|이|를|을|와|과|의)?/g, (m, g, josa = "") => {
                const n = names[g];
                if (!n) return m;
                const fixed = { "는": "은", "가": "이", "를": "을", "와": "과" }[josa] ?? josa;
                return `${n}님${fixed}`;
            });
    }

    function loveSentence(t) {
        const love = t.love.replace(/[.]$/, "");
        return `애정은 주로 ${esc(love)}${euro(love)} 표현하는 편이에요.`;
    }

    function axisEffort(me, you, a, b) {
        const ax = AXES.find(x => a[x.i] !== b[x.i]);
        if (!ax) return null;
        const aName = a[ax.i] === ax.a ? me : you;
        const bName = aName === me ? you : me;
        return { title: ax.title, text: ax.text(esc(aName), esc(bName)) };
    }

    // 관계 단계에 따라 노력 2가지를 고른다
    function stageEfforts(stage, ctx) {
        const { me, you, a, b, t1, t2, c, p } = ctx;
        const y = esc(you), m = esc(me);
        const fallback = c.efforts.map(e => ({ title: "두 분이 함께 정해 볼 약속", text: p(e) }));
        if (stage === "before") {
            const g = D.groups[t2.group];
            return [
                { title: "첫 대화는 이 질문부터",
                  text: `${g.goodQ.map(q => `“${esc(q)}”`).join(" ")}처럼 ${y}님이 편하게 이야기할 수 있는 질문부터 시작해 보세요. ${esc(g.avoidQ)}${eunNeun(g.avoidQ)} 처음엔 피하는 게 좋아요. 첫 데이트 장소로는 ${esc(g.place)}${iGa(g.place)} 잘 맞아요.` },
                axisEffort(me, you, a, b) || fallback[0]
            ];
        }
        if (stage === "some") {
            const line = c.confess.find(x => x.who.startsWith(t1.group)) || c.confess[0];
            return [
                { title: "고백 타이밍은 썸 3주차 무렵",
                  text: `<b>썸 3주차쯤 흔한 모습:</b> ${p(c.week3.what)}.<br>이때가 마음을 한 문장으로 전하기 좋은 타이밍이에요. <b>해 볼 것:</b> ${p(c.week3.tip)}` },
                { title: `${m}님이 먼저 말한다면`, text: `${p(line.line)}처럼, 이유보다 마음을 한 줄 먼저 전해 보세요.` }
            ];
        }
        return [
            { title: `${y}님과 다툰 뒤엔`,
              text: `${y}님은 ${esc(D.makeup[b[0]])} ${esc(D.makeup[b[2]])}` },
            { title: "서운할 때 이렇게 말해 보세요",
              text: `${p(c.hurt.scene)} 같은 순간엔, ${p(c.hurt.say)}라고 말해 보세요.` }
        ];
    }

    function build(input) {
        const { me, you, myType: a, yourType: b, stage } = input;
        const t1 = D.types[a], t2 = D.types[b];
        const key = [t1.group, t2.group].sort((x, z) => GROUP_ORDER.indexOf(x) - GROUP_ORDER.indexOf(z)).join("+");
        const c = D.combos[key];
        const names = t1.group === t2.group ? {} : { [t1.group]: esc(me), [t2.group]: esc(you) };
        const p = s => personalize(esc(s), names);
        const sc = scores(a, b, stage);
        const efforts = stageEfforts(stage, { me, you, a, b, t1, t2, c, p });
        return { ...input, t1, t2, c, p, sc, efforts };
    }

    function render(r) {
        const m = esc(r.me), y = esc(r.you);
        out.innerHTML = `
<div class="match-card match-score-card">
  <p class="match-pair">${m}님 <span>${r.myType}</span> <i aria-hidden="true">♥</i> ${y}님 <span>${r.yourType}</span></p>
  <p class="match-stage">${STAGE_LABEL[r.stage]}</p>
  <p class="match-score"><b data-target="${r.sc.total}">${r.sc.total}</b><small>점</small></p>
  <div class="match-bar"><span style="width:${r.sc.total}%"></span></div>
  <h2>${esc(r.c.title)}</h2>
  <p>${r.p(r.c.oneline)}</p>
</div>
<div class="match-card">
  <p class="match-label">항목별 궁합</p>
  ${SUBS.map((s, i) => `<div class="match-sub"><span>${s.label}${r.sc.sub[i] < EFFORT_POINT ? '<em class="match-point">노력 포인트</em>' : ""}</span><div class="match-sub-bar"><i style="width:${r.sc.sub[i]}%"></i></div><b>${r.sc.sub[i]}</b></div>`).join("")}
  <p class="match-sub-summary">${scoreSummary(r)}</p>
</div>
<div class="match-card">
  <p class="match-label">연애 스타일</p>
  <h3>${m}님은 ‘${esc(r.t1.nick)}’</h3>
  <p>${esc(r.t1.style)} ${loveSentence(r.t1)}</p>
  <h3>${y}님은 ‘${esc(r.t2.nick)}’</h3>
  <p>${esc(r.t2.style)} ${loveSentence(r.t2)}</p>
  <div class="match-note"><b>${y}님에게 다가갈 땐</b> ${esc(r.t2.approach)}</div>
</div>
<div class="match-card">
  <p class="match-label">두 사람이 만나면</p>
  <p><b>끌리는 이유</b><br>${r.p(r.c.pull)}</p>
  <p><b>조심할 점</b><br>${r.p(r.c.trap)}</p>
</div>
<div class="match-card">
  <p class="match-label">${STAGE_LABEL[r.stage]}인 두 분을 위한 노력 2가지</p>
  ${r.efforts.map((e, i) => `<div class="match-effort"><span>${i + 1}</span><div><h3>${e.title}</h3><p>${e.text}</p></div></div>`).join("")}
</div>
<div class="match-card match-share">
  <p><b>결과 카드를 저장해서 인스타 스토리에 올려 보세요</b></p>
  <button type="button" class="love-cta" id="match-save">결과 카드 저장하기 <span aria-hidden="true">↓</span></button>
</div>
<div class="match-card match-cta">
  <p>${m}님과 ${y}님 조합의 <b>첫 만남·연락·썸·고백 문장</b>까지 담은 전체 가이드</p>
  <a class="love-cta" href="/love#books">MBTI 연애 가이드 전체 보기 <span aria-hidden="true">→</span></a>
  <div class="match-links">
    <a href="https://www.instagram.com/slowblossom.daegu/" target="_blank" rel="noopener noreferrer">인스타 팔로우</a>
    <a href="/">로테이션 소개팅 보기</a>
    <button type="button" id="match-again">다시 해 보기</button>
  </div>
</div>
<p class="match-fine">재미로 보는 참고 지표입니다. MBTI는 성향의 경향일 뿐, 두 사람의 관계를 다 설명하지 못해요.<br>MBTI는 The Myers-Briggs Company의 상표이며, 슬로우블러썸은 해당 회사와 관련이 없어요.</p>`;
        out.hidden = false;
        out.scrollIntoView({ behavior: "smooth", block: "start" });

        const num = out.querySelector("[data-target]");
        const start = performance.now();
        function tick(now) {
            const k = Math.min(1, Math.max(0, (now - start) / 900));
            num.textContent = Math.round(r.sc.total * (1 - Math.pow(1 - k, 3)));
            if (k < 1) requestAnimationFrame(tick);
        }
        requestAnimationFrame(tick);

        document.getElementById("match-again").addEventListener("click", () => {
            out.hidden = true;
            form.reset();
            form.hidden = false;
            showStep(0);
            form.scrollIntoView({ behavior: "smooth", block: "start" });
        });
        document.getElementById("match-save").addEventListener("click", () => saveCard(r));
    }

    // ── 결과 카드 이미지 (인스타 스토리 1080×1920) ──
    function wrapLines(ctx, text, maxW) {
        const lines = [];
        let line = "";
        for (const word of text.split(" ")) {
            const next = line ? line + " " + word : word;
            if (ctx.measureText(next).width > maxW && line) { lines.push(line); line = word; } else line = next;
        }
        if (line) lines.push(line);
        return lines;
    }

    async function drawCard(r) {
        try { await document.fonts.load('700 80px "Gowun Batang"'); } catch { /* 기본 글꼴로 그린다 */ }
        const W = 1080, H = 1920;
        const cv = document.createElement("canvas");
        cv.width = W; cv.height = H;
        const ctx = cv.getContext("2d");
        const bg = ctx.createLinearGradient(0, 0, W * 0.4, H);
        bg.addColorStop(0, "#22503d"); bg.addColorStop(1, "#173628");
        ctx.fillStyle = bg; ctx.fillRect(0, 0, W, H);
        const glow = ctx.createRadialGradient(W * 0.9, 120, 0, W * 0.9, 120, 620);
        glow.addColorStop(0, "rgba(242,184,198,0.5)"); glow.addColorStop(1, "rgba(242,184,198,0)");
        ctx.fillStyle = glow; ctx.fillRect(0, 0, W, H);

        const serif = '"Gowun Batang", "Noto Serif KR", serif';
        const sans = '"Pretendard", "Apple SD Gothic Neo", "Malgun Gothic", sans-serif';
        ctx.textAlign = "center";
        ctx.fillStyle = "#f7f1e8";
        ctx.font = `700 34px ${sans}`;
        ctx.fillText("S L O W   B L O S S O M", W / 2, 190);
        ctx.fillStyle = "#f2b8c6";
        ctx.font = `700 40px ${sans}`;
        ctx.fillText("MBTI 연애 궁합", W / 2, 270);

        ctx.fillStyle = "#f7f1e8";
        ctx.font = `700 54px ${sans}`;
        ctx.fillText(`${r.me} ${r.myType}  ♥  ${r.you} ${r.yourType}`, W / 2, 430);
        ctx.fillStyle = "rgba(247,241,232,0.7)";
        ctx.font = `500 36px ${sans}`;
        ctx.fillText(STAGE_LABEL[r.stage], W / 2, 495);

        ctx.fillStyle = "#f7f1e8";
        ctx.font = `700 280px ${serif}`;
        const scoreText = String(r.sc.total);
        const sw = ctx.measureText(scoreText).width;
        ctx.fillText(scoreText, W / 2 - 30, 810);
        ctx.font = `700 80px ${serif}`;
        ctx.textAlign = "left";
        ctx.fillText("점", W / 2 - 30 + sw / 2 + 12, 810);
        ctx.textAlign = "center";

        ctx.font = `700 58px ${serif}`;
        wrapLines(ctx, r.c.title, W - 200).slice(0, 2).forEach((l, i) => ctx.fillText(l, W / 2, 960 + i * 80));

        const x0 = 150, barX = 380, barW = 440;
        r.sc.sub.forEach((v, i) => {
            const y = 1200 + i * 140;
            ctx.textAlign = "left";
            ctx.fillStyle = "#f7f1e8";
            ctx.font = `600 42px ${sans}`;
            ctx.fillText(SUBS[i].label, x0, y + 14);
            ctx.fillStyle = "rgba(247,241,232,0.18)";
            ctx.beginPath(); ctx.roundRect(barX, y - 12, barW, 24, 12); ctx.fill();
            ctx.fillStyle = "#f2b8c6";
            ctx.beginPath(); ctx.roundRect(barX, y - 12, barW * v / 100, 24, 12); ctx.fill();
            ctx.textAlign = "right";
            ctx.fillStyle = "#f7f1e8";
            ctx.font = `700 42px ${sans}`;
            ctx.fillText(String(v), W - 150, y + 14);
        });

        ctx.textAlign = "center";
        ctx.fillStyle = "#f2b8c6";
        ctx.font = `700 40px ${sans}`;
        ctx.fillText("나도 궁합 보기", W / 2, 1700);
        ctx.fillStyle = "rgba(247,241,232,0.8)";
        ctx.font = `500 36px ${sans}`;
        ctx.fillText("slowblossom.onrender.com/love/match", W / 2, 1765);
        ctx.fillText("@slowblossom.daegu", W / 2, 1820);
        ctx.fillStyle = "rgba(247,241,232,0.5)";
        ctx.font = `500 26px ${sans}`;
        ctx.fillText("재미로 보는 참고 지표입니다", W / 2, 1880);
        return cv;
    }

    async function saveCard(r) {
        const cv = await drawCard(r);
        const blob = await new Promise(res => cv.toBlob(res, "image/png"));
        if (!blob) return;
        const file = new File([blob], "slowblossom-mbti-match.png", { type: "image/png" });
        if (navigator.canShare && navigator.canShare({ files: [file] })) {
            try { await navigator.share({ files: [file], title: "MBTI 연애 궁합" }); return; } catch (e) { if (e.name === "AbortError") return; }
        }
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url; link.download = file.name;
        document.body.appendChild(link); link.click(); link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 2000);
    }

    // ── 한 화면에 질문 하나 ──
    const steps = [...form.querySelectorAll(".match-step")];
    const back = form.querySelector(".match-back");
    const bar = form.querySelector(".match-progress span");
    let current = 0;

    function showStep(i) {
        current = i;
        steps.forEach((s, k) => { s.hidden = k !== i; });
        back.hidden = i === 0;
        bar.style.width = `${(i / steps.length) * 100}%`;
        form.querySelectorAll("[data-name]").forEach(el => { el.textContent = form.elements[el.dataset.name].value.trim(); });
        const input = steps[i].querySelector("input:not([type=radio])");
        if (input) setTimeout(() => input.focus({ preventScroll: true }), 50);
    }

    function nameStepNext() {
        const input = steps[current].querySelector("input:not([type=radio])");
        if (!input) return;
        if (!input.value.trim()) { input.reportValidity(); return; }
        showStep(current + 1);
    }

    form.querySelectorAll(".match-next").forEach(btn => btn.addEventListener("click", nameStepNext));
    form.addEventListener("submit", ev => { ev.preventDefault(); nameStepNext(); });
    back.addEventListener("click", () => showStep(Math.max(0, current - 1)));
    // change가 아닌 click: 이전으로 돌아가 같은 답을 다시 눌러도 다음으로 넘어가게
    form.addEventListener("click", ev => {
        if (ev.target.type !== "radio") return;
        if (ev.target.name === "stage") setTimeout(analyze, 200);
        else setTimeout(() => showStep(current + 1), 200);
    });

    // ── 분석 중 연출 ──
    function analyze() {
        const f = new FormData(form);
        const input = {
            me: String(f.get("me") || "").trim(), you: String(f.get("you") || "").trim(),
            myType: String(f.get("myType") || ""), yourType: String(f.get("yourType") || ""),
            stage: String(f.get("stage") || "")
        };
        if (!input.me || !input.you || !D.types[input.myType] || !D.types[input.yourType] || !STAGE_LABEL[input.stage]) {
            showStep(0);
            return;
        }
        const result = build(input);
        const msgs = [
            `${input.me}님의 연애 스타일을 읽는 중…`,
            `${input.you}님과의 대화 리듬을 맞춰 보는 중…`,
            "두 사람의 궁합 점수를 계산하는 중…"
        ];
        const text = loading.querySelector("p");
        form.hidden = true;
        loading.hidden = false;
        loading.scrollIntoView({ behavior: "smooth", block: "center" });
        msgs.forEach((msg, i) => setTimeout(() => { text.textContent = msg; }, i * 800));
        setTimeout(() => { loading.hidden = true; render(result); }, msgs.length * 800);
    }

    // 테스트·점검용: 화면 연출 없이 결과를 계산한다
    window.__mbtiMatch = { build, render, drawCard };
    showStep(0);
})();

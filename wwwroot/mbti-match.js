// MBTI 궁합 테스트 (/love/match). 입력한 이름은 서버로 보내지 않고 이 화면 안에서만 쓴다.
(function () {
    const D = window.MBTI_MATCH;
    const form = document.getElementById("match-form");
    const out = document.getElementById("match-result");
    if (!D || !form || !out) return;

    const GROUP_ORDER = ["NT", "NF", "SJ", "SP"];
    // 그룹 조합별 기본 점수 (전자책 '궁합' 장: 나쁜 조합은 없고, 편한 점과 부딪히는 지점이 다를 뿐)
    const BASE = {
        "NT+NT": 84, "NT+NF": 90, "NT+SJ": 82, "NT+SP": 86, "NF+NF": 88,
        "NF+SJ": 87, "NF+SP": 89, "SJ+SJ": 85, "SJ+SP": 86, "SP+SP": 83
    };
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

    const esc = s => String(s).replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

    // 같은 입력이면 늘 같은 결과가 나오도록 이름으로 만든 작은 숫자
    function nameSeed(s) {
        let h = 7;
        for (const ch of s) h = (h * 31 + ch.codePointAt(0)) % 100003;
        return h;
    }

    function score(me, you, myType, yourType, key) {
        let s = BASE[key];
        if (myType[0] === yourType[0]) s += 1;
        if (myType[1] === yourType[1]) s += 2;
        if (myType[2] !== yourType[2]) s += 1;
        if (myType[3] !== yourType[3]) s += 1;
        const pair = [me + myType, you + yourType].sort().join("|");
        s += nameSeed(pair) % 5;
        return Math.max(72, Math.min(98, s));
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

    // 받침에 따라 '으로/로'
    function euro(word) {
        const code = word.charCodeAt(word.length - 1) - 0xac00;
        if (code < 0 || code > 11171) return "로";
        const jong = code % 28;
        return jong === 0 || jong === 8 ? "로" : "으로";
    }

    function loveSentence(t) {
        const love = t.love.replace(/[.]$/, "");
        return `애정은 주로 ${esc(love)}${euro(love)} 표현하는 편이에요.`;
    }

    function render(me, you, myType, yourType) {
        const t1 = D.types[myType], t2 = D.types[yourType];
        const key = [t1.group, t2.group].sort((a, b) => GROUP_ORDER.indexOf(a) - GROUP_ORDER.indexOf(b)).join("+");
        const c = D.combos[key];
        const names = t1.group === t2.group ? {} : { [t1.group]: esc(me), [t2.group]: esc(you) };
        const p = s => personalize(esc(s), names);
        const sc = score(me, you, myType, yourType, key);

        const efforts = [];
        const diff = AXES.find(ax => myType[ax.i] !== yourType[ax.i]);
        if (diff) {
            const aName = myType[diff.i] === diff.a ? me : you;
            const bName = aName === me ? you : me;
            efforts.push({ title: diff.title, text: diff.text(esc(aName), esc(bName)) });
        }
        for (const e of c.efforts) {
            if (efforts.length >= 2) break;
            efforts.push({ title: "두 분이 함께 정해 볼 약속", text: p(e) });
        }

        const m = esc(me), y = esc(you);
        out.innerHTML = `
<div class="match-card match-score-card">
  <p class="match-pair">${m}님 <span>${myType}</span> <i aria-hidden="true">♥</i> ${y}님 <span>${yourType}</span></p>
  <p class="match-score"><b data-target="${sc}">0</b><small>점</small></p>
  <div class="match-bar"><span style="width:${sc}%"></span></div>
  <h2>${esc(c.title)}</h2>
  <p>${p(c.oneline)}</p>
</div>
<div class="match-card">
  <p class="match-label">연애 스타일</p>
  <h3>${m}님은 ‘${esc(t1.nick)}’</h3>
  <p>${esc(t1.style)} ${loveSentence(t1)}</p>
  <h3>${y}님은 ‘${esc(t2.nick)}’</h3>
  <p>${esc(t2.style)} ${loveSentence(t2)}</p>
  <div class="match-note"><b>${y}님에게 다가갈 땐</b> ${esc(t2.approach)}</div>
</div>
<div class="match-card">
  <p class="match-label">두 사람이 만나면</p>
  <p><b>끌리는 이유</b><br>${p(c.pull)}</p>
  <p><b>조심할 점</b><br>${p(c.trap)}</p>
</div>
<div class="match-card">
  <p class="match-label">관계를 위한 노력 2가지</p>
  ${efforts.map((e, i) => `<div class="match-effort"><span>${i + 1}</span><div><h3>${esc(e.title)}</h3><p>${e.text}</p></div></div>`).join("")}
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
<p class="match-fine">재미로 보는 궁합이에요. MBTI는 성향의 경향일 뿐, 두 사람의 관계를 다 설명하지 못해요.<br>MBTI는 The Myers-Briggs Company의 상표이며, 슬로우블러썸은 해당 회사와 관련이 없어요.</p>`;
        out.hidden = false;
        form.hidden = true;
        out.scrollIntoView({ behavior: "smooth", block: "start" });

        const num = out.querySelector("[data-target]");
        num.textContent = sc; // 애니메이션이 돌지 않는 환경에서도 점수는 보이게
        const start = performance.now();
        function tick(now) {
            const k = Math.min(1, (now - start) / 900);
            num.textContent = Math.round(sc * (1 - Math.pow(1 - k, 3)));
            if (k < 1) requestAnimationFrame(tick);
        }
        requestAnimationFrame(tick);
        document.getElementById("match-again").addEventListener("click", () => {
            out.hidden = true;
            form.hidden = false;
            form.scrollIntoView({ behavior: "smooth", block: "start" });
        });
    }

    form.addEventListener("submit", ev => {
        ev.preventDefault();
        const f = new FormData(form);
        const me = String(f.get("me") || "").trim(), you = String(f.get("you") || "").trim();
        const myType = String(f.get("myType") || ""), yourType = String(f.get("yourType") || "");
        if (!me || !you || !D.types[myType] || !D.types[yourType]) {
            form.reportValidity();
            return;
        }
        render(me, you, myType, yourType);
    });
})();

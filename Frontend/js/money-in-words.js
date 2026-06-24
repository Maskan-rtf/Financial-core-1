(function () {
  const ONES = ["", "یک", "دو", "سه", "چهار", "پنج", "شش", "هفت", "هشت", "نه"];
  const TEENS = ["ده", "یازده", "دوازده", "سیزده", "چهارده", "پانزده", "شانزده", "هفده", "هجده", "نوزده"];
  const TENS = ["", "ده", "بیست", "سی", "چهل", "پنجاه", "شصت", "هفتاد", "هشتاد", "نود"];
  const HUNDREDS = ["", "یکصد", "دویست", "سیصد", "چهارصد", "پانصد", "ششصد", "هفتصد", "هشتصد", "نهصد"];
  const SCALES = ["", "هزار", "میلیون", "میلیارد", "تریلیون"];

  function threeDigitsToWords(n) {
    const num = Number(n) || 0;
    if (num <= 0) return "";
    const h = Math.floor(num / 100);
    const t = Math.floor((num % 100) / 10);
    const o = num % 10;
    const parts = [];
    if (h > 0) parts.push(HUNDREDS[h]);
    if (t === 1) parts.push(TEENS[o]);
    else {
      if (t > 0) parts.push(TENS[t]);
      if (o > 0) parts.push(ONES[o]);
    }
    return parts.join(" و ");
  }

  function integerToPersianWords(value) {
    const n = Math.floor(Math.abs(Number(value) || 0));
    if (n === 0) return "صفر";
    const chunks = [];
    let remaining = n;
    let scaleIndex = 0;
    while (remaining > 0) {
      const chunk = remaining % 1000;
      if (chunk > 0) {
        const chunkWords = threeDigitsToWords(chunk);
        const scale = SCALES[scaleIndex];
        chunks.unshift(scale ? chunkWords + " " + scale : chunkWords);
      }
      remaining = Math.floor(remaining / 1000);
      scaleIndex += 1;
    }
    return chunks.join(" و ").trim();
  }

  function formatRial(amount) {
    const n = Number(amount);
    if (!Number.isFinite(n) || n <= 0) return null;
    const rounded = Math.round(n);
    return integerToPersianWords(rounded) + " ریال";
  }

  window.MoneyInWords = {
    formatRial,
    integerToPersianWords,
  };
})();

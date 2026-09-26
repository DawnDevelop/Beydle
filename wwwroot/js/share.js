// Draws the result card as a PNG and hands it to the share sheet (touch devices), the clipboard, or a download.
// Called from ShareService; returns "shared", "copied", "saved", "cancelled" or "failed". Everything up to the
// share or clipboard call is synchronous so it still counts as part of the player's click (Safari insists).
window.beydleShareImage = async function (card) {
    const colors = { bg: "#0b0d14", panel: "#161a27", text: "#e8ebf5", muted: "#8b93ad", exact: "#22b573", partial: "#e3b341", miss: "#2a3043", accent: "#5b8cff" };
    const hit = [colors.exact, colors.partial, colors.miss]; // matches the Hit enum order
    const scale = 2, width = 600, pad = 32, cell = 40, gap = 8, rowGap = 8;
    const gridWidth = card.columns * cell + (card.columns - 1) * gap;
    const headerHeight = 96, sectionGap = 28, imageRowHeight = 30;
    const height = pad + headerHeight + card.rows.length * (cell + rowGap) + sectionGap + 24 + imageRowHeight + sectionGap + 20 + pad;

    const canvas = document.createElement("canvas");
    canvas.width = width * scale;
    canvas.height = height * scale;
    const ctx = canvas.getContext("2d");
    ctx.scale(scale, scale);
    const font = (weight, size) => `${weight} ${size}px Inter, system-ui, "Segoe UI", sans-serif`;
    const rounded = (x, y, w, h, r, fill) => { ctx.fillStyle = fill; ctx.beginPath(); ctx.roundRect(x, y, w, h, r); ctx.fill(); };

    ctx.fillStyle = colors.bg;
    ctx.fillRect(0, 0, width, height);
    const glow = ctx.createRadialGradient(width * .15, -40, 0, width * .15, -40, 420);
    glow.addColorStop(0, "rgba(91, 140, 255, .22)");
    glow.addColorStop(1, "rgba(91, 140, 255, 0)");
    ctx.fillStyle = glow;
    ctx.fillRect(0, 0, width, height);

    let y = pad;
    ctx.textBaseline = "top";
    ctx.fillStyle = colors.text;
    ctx.font = font(800, 30);
    ctx.fillText(card.title, pad, y);
    ctx.fillStyle = colors.muted;
    ctx.font = font(600, 16);
    ctx.fillText(card.summary, pad, y + 44);
    if (card.xtremeMode) {
        ctx.font = font(700, 13);
        const label = "XTREME MODE", w = ctx.measureText(label).width + 20;
        rounded(width - pad - w, y + 6, w, 26, 13, "rgba(227, 179, 65, .16)");
        ctx.fillStyle = colors.partial;
        ctx.fillText(label, width - pad - w + 10, y + 12);
    }
    y += headerHeight;

    const left = pad;
    ctx.font = font(600, 14);
    card.rows.forEach((row, i) => {
        row.forEach((h, c) => rounded(left + c * (cell + gap), y, cell, cell, 8, hit[h]));
        const note = card.left[i];
        if (note) {
            ctx.fillStyle = colors.muted;
            ctx.fillText(note, left + gridWidth + 18, y + cell / 2 - 8);
        }
        y += cell + rowGap;
    });

    y += sectionGap - rowGap;
    ctx.fillStyle = colors.muted;
    ctx.font = font(700, 12);
    ctx.fillText("ROUND 2", pad, y);
    y += 24;
    card.picture.forEach((won, i) => rounded(left + i * (imageRowHeight + 6), y, imageRowHeight, imageRowHeight, 7, won ? colors.exact : colors.miss));
    y += imageRowHeight + sectionGap;

    ctx.fillStyle = colors.accent;
    ctx.font = font(700, 16);
    ctx.fillText(card.site, pad, y);

    const bytes = atob(canvas.toDataURL("image/png").split(",")[1]);
    const data = new Uint8Array(bytes.length);
    for (let i = 0; i < bytes.length; i++) data[i] = bytes.charCodeAt(i);
    const blob = new Blob([data], { type: "image/png" });
    const file = new File([blob], "beydle.png", { type: "image/png" });

    if (matchMedia("(pointer: coarse)").matches && navigator.canShare?.({ files: [file] })) {
        try {
            await navigator.share({ files: [file] });
            return "shared";
        } catch (e) {
            if (e.name === "AbortError") return "cancelled";
        }
    }
    if (window.ClipboardItem && navigator.clipboard?.write) {
        try {
            await navigator.clipboard.write([new ClipboardItem({ "image/png": blob })]);
            return "copied";
        } catch { /* fall through to a download */ }
    }
    try {
        const a = document.createElement("a");
        a.href = URL.createObjectURL(blob);
        a.download = "beydle.png";
        a.click();
        setTimeout(() => URL.revokeObjectURL(a.href), 10000);
        return "saved";
    } catch {
        return "failed";
    }
};

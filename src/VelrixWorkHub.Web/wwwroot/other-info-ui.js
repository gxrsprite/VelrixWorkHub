(() => {
    const rawMarker = /OtherInfo|扩展信息/i;

    const hideLabel = label => {
        if (rawMarker.test(label.textContent ?? "") && label.querySelector("input, textarea, select")) {
            label.hidden = true;
        }
    };

    const hideRawOtherInfo = (root = document) => {
        if (root instanceof Element && root.matches("label")) {
            hideLabel(root);
        }

        root.querySelectorAll?.("label").forEach(hideLabel);

        const hideInputOwner = input => {
            const marker = `${input.getAttribute("aria-label") ?? ""} ${input.getAttribute("placeholder") ?? ""}`;
            if (rawMarker.test(marker)) {
                (input.closest("label") ?? input.parentElement)?.setAttribute("hidden", "");
            }
        };

        if (root instanceof Element && root.matches("input, textarea, select")) {
            hideInputOwner(root);
        }

        root.querySelectorAll?.("input, textarea, select").forEach(hideInputOwner);

        root.querySelectorAll?.(".customer-note, p, span, div").forEach(element => {
            const text = (element.textContent ?? "").trim();
            if (element.children.length === 0 && /^(OtherInfo|扩展信息)(\s+(JSON|JSON 对象))?\s*[:：]/i.test(text)) {
                element.hidden = true;
            }
        });
    };

    const start = () => {
        hideRawOtherInfo();
        new MutationObserver(records => records.forEach(record => record.addedNodes.forEach(node => {
            if (node.nodeType === Node.ELEMENT_NODE) {
                hideRawOtherInfo(node);
            }
        }))).observe(document.body, { childList: true, subtree: true });
    };

    document.readyState === "loading"
        ? document.addEventListener("DOMContentLoaded", start, { once: true })
        : start();
})();

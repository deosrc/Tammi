// The selected commit's details beside a git graph.

// Copies `text` to the clipboard. The Clipboard API isn't always there in the app's web view (it needs a secure
// context), so it falls back to copying from a hidden text area. Returns whether it was copied.
export async function copyText(text) {
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text);
      return true;
    }
  } catch {
    // Not allowed here: try the other way.
  }

  const area = document.createElement("textarea");
  area.value = text;
  area.setAttribute("readonly", "");
  area.style.position = "fixed";
  area.style.opacity = "0";
  document.body.appendChild(area);
  const focused = document.activeElement;
  area.select();
  try {
    return document.execCommand("copy");
  } catch {
    return false;
  } finally {
    area.remove();
    focused?.focus?.();
  }
}

// The body's default height, in lines, and the least it shows. The rest of the panel keeps at least `minLowerHeight`.
const defaultLines = 8;
const minLines = 3;
const minLowerHeight = 120;
const keyboardStep = 16;

// Makes `splitter`, below the message's body `message` in the details `root`, resize the body: drag it, or focus it and
// use Up and Down. Double-clicking it goes back to the default height.
//
// The body starts at `savedHeight` (the default when it's null). That's the height the user chose, in pixels: when the
// panel is too short for it, the body is shown shorter (but never less than three lines), and goes back to it when
// there's room. Only the user's resizing changes it, and then `dotNet.OnMessageHeightChanged` is told the new height
// (null for the default), to keep it for next time.
export function attachMessageSplitter(root, message, splitter, savedHeight, dotNet) {
  const linesHeight = (lines) => {
    const style = getComputedStyle(message);
    const lineHeight = parseFloat(style.lineHeight) || parseFloat(style.fontSize) * 1.4;
    const frame = parseFloat(style.paddingTop) + parseFloat(style.paddingBottom)
      + parseFloat(style.borderTopWidth) + parseFloat(style.borderBottomWidth);
    return Math.ceil(lines * lineHeight + frame);
  };

  const minHeight = () => linesHeight(minLines);
  const maxHeight = () => {
    const room = root.getBoundingClientRect().bottom - message.getBoundingClientRect().top
      - splitter.offsetHeight - minLowerHeight;
    return Math.max(minHeight(), Math.floor(room));
  };

  let isDefault = savedHeight === null || savedHeight === undefined;
  let chosenHeight = isDefault ? linesHeight(defaultLines) : savedHeight;

  // Shows the chosen height, as far as the panel has room for it.
  const show = () => {
    const min = minHeight();
    const max = maxHeight();
    const shown = Math.round(Math.min(Math.max(chosenHeight, min), max));
    root.style.setProperty("--git-commit-body-height", `${shown}px`);
    splitter.setAttribute("aria-valuenow", String(shown));
    splitter.setAttribute("aria-valuemin", String(min));
    splitter.setAttribute("aria-valuemax", String(max));
  };

  // The user chose a height: it's limited to what fits now, which is what they see.
  const choose = (height) => {
    chosenHeight = Math.round(Math.min(Math.max(height, minHeight()), maxHeight()));
    isDefault = false;
    show();
  };

  const save = () => {
    dotNet.invokeMethodAsync("OnMessageHeightChanged", isDefault ? null : chosenHeight).catch(() => {
      // The panel has gone: the height is lost, which only means the body opens at its old height next time.
    });
  };

  let dragStartHeight = null;

  const onPointerMove = (e) => {
    // The body starts at its top edge, so its height is the distance from there to the pointer.
    choose(e.clientY - message.getBoundingClientRect().top - splitter.offsetHeight / 2);
  };

  const onPointerUp = (e) => {
    splitter.releasePointerCapture(e.pointerId);
    splitter.classList.remove("dragging");
    splitter.removeEventListener("pointermove", onPointerMove);
    splitter.removeEventListener("pointerup", onPointerUp);
    splitter.removeEventListener("pointercancel", onPointerUp);
    if (chosenHeight !== dragStartHeight) {
      save();
    }
  };

  const onPointerDown = (e) => {
    if (e.button !== 0) {
      return;
    }

    e.preventDefault();
    dragStartHeight = chosenHeight;
    splitter.setPointerCapture(e.pointerId);
    splitter.classList.add("dragging");
    splitter.addEventListener("pointermove", onPointerMove);
    splitter.addEventListener("pointerup", onPointerUp);
    splitter.addEventListener("pointercancel", onPointerUp);
  };

  // Keys move from the height shown. It's saved once the key is let go, not at every step of a held key.
  const isResizeKey = (e) => e.key === "ArrowUp" || e.key === "ArrowDown";
  let keyStartHeight = null;

  const onKeyDown = (e) => {
    if (!isResizeKey(e)) {
      return;
    }

    e.preventDefault();
    keyStartHeight ??= chosenHeight;
    const shown = Number(splitter.getAttribute("aria-valuenow"));
    choose(shown + (e.key === "ArrowDown" ? keyboardStep : -keyboardStep));
  };

  const onKeyUp = (e) => {
    if (isResizeKey(e) && keyStartHeight !== null) {
      if (chosenHeight !== keyStartHeight) {
        save();
      }

      keyStartHeight = null;
    }
  };

  const onDoubleClick = () => {
    chosenHeight = linesHeight(defaultLines);
    isDefault = true;
    show();
    save();
  };

  // Keep the body within its limits as the panel changes size.
  const observer = new ResizeObserver(show);

  splitter.addEventListener("pointerdown", onPointerDown);
  splitter.addEventListener("keydown", onKeyDown);
  splitter.addEventListener("keyup", onKeyUp);
  splitter.addEventListener("dblclick", onDoubleClick);
  observer.observe(root);
  show();

  return {
    dispose() {
      observer.disconnect();
      splitter.removeEventListener("pointerdown", onPointerDown);
      splitter.removeEventListener("keydown", onKeyDown);
      splitter.removeEventListener("keyup", onKeyUp);
      splitter.removeEventListener("dblclick", onDoubleClick);
      splitter.removeEventListener("pointermove", onPointerMove);
      splitter.removeEventListener("pointerup", onPointerUp);
      splitter.removeEventListener("pointercancel", onPointerUp);
      root.style.removeProperty("--git-commit-body-height");
    },
  };
}

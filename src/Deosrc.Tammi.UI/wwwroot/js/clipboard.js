// Puts text on the clipboard. The Clipboard API needs a secure context, so where it's missing or refused, the
// 'copy' command copies the text from a hidden text area instead.
export async function copyText(text) {
  if (navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      // Fall back to the 'copy' command.
    }
  }

  const area = document.createElement('textarea');
  area.value = text;
  area.setAttribute('readonly', '');
  area.style.position = 'fixed';
  area.style.opacity = '0';
  document.body.appendChild(area);
  area.select();
  try {
    return document.execCommand('copy');
  } finally {
    area.remove();
  }
}

// Copies the text an element shows, as it's laid out (e.g. with a line break between the rows of a list). A
// description list is copied a pair to a line, e.g. "Version: 1.2.3".
export function copyElementText(element) {
  const list = element.matches('dl') ? element : element.querySelector(':scope > dl');
  if (list) {
    const lines = [];
    for (const term of list.querySelectorAll(':scope > dt')) {
      const description = term.nextElementSibling;
      const value = description?.matches('dd') ? description.innerText.trim() : '';
      lines.push(`${term.innerText.trim()}: ${value}`);
    }
    return copyText(lines.join('\n'));
  }

  return copyText(element.innerText.trim());
}

// Shows "Copied!" in a tooltip above an element (or the element with that id) for a moment, e.g. once its text is
// copied. It's a popover, so it shows in the top layer, unclipped by a panel that scrolls. Fluent's tooltip only opens
// on hover, so it can't be used.
let copiedTip;
let copiedTimer;

export function showCopied(target) {
  const element = typeof target === 'string' ? document.getElementById(target) : target;
  if (!element?.isConnected) {
    return;
  }

  if (!copiedTip) {
    copiedTip = document.createElement('div');
    copiedTip.className = 'copied-tip';
    copiedTip.popover = 'manual';
    // A screen reader is told by the copying element's own status, so it isn't told twice.
    copiedTip.setAttribute('aria-hidden', 'true');
    copiedTip.textContent = 'Copied!';
    document.body.appendChild(copiedTip);
  }

  clearTimeout(copiedTimer);
  if (copiedTip.matches(':popover-open')) {
    copiedTip.hidePopover();
  }
  copiedTip.showPopover();

  // Centred above the element, or below it when there's no room above, and kept inside the window.
  const gap = 6;
  const box = element.getBoundingClientRect();
  const width = copiedTip.offsetWidth;
  const height = copiedTip.offsetHeight;
  const left = Math.min(Math.max(gap, box.left + box.width / 2 - width / 2), window.innerWidth - width - gap);
  const top = box.top - height - gap >= 0 ? box.top - height - gap : box.bottom + gap;
  copiedTip.style.left = `${left}px`;
  copiedTip.style.top = `${top}px`;

  copiedTimer = setTimeout(() => copiedTip.hidePopover(), 1500);
}

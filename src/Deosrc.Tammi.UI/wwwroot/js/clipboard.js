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

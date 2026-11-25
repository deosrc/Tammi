// Keeps the two panes of a side by side diff scrolled the same distance sideways. Scroll events don't bubble, so the
// body listens in the capture phase, which also covers panes that are drawn again when the diff is read again.

export function attach(body) {
  if (!body || body.dataset.scrollSync) {
    return;
  }

  body.dataset.scrollSync = 'true';

  // What each pane was last set to by this script. The scroll event that setting raises is an echo, not the user
  // scrolling, so it's ignored. Ignoring it by time instead let a late echo, carrying an older position, move the
  // pane under the cursor back, which made it judder.
  const assigned = new WeakMap();
  body.addEventListener('scroll', event => {
    const pane = event.target;
    if (!(pane instanceof HTMLElement) || !pane.classList.contains('git-diff-pane')) {
      return;
    }

    const echo = assigned.get(pane);
    if (echo !== undefined) {
      assigned.delete(pane);
      if (Math.abs(echo - pane.scrollLeft) < 1) {
        return;
      }
    }

    for (const other of pane.parentElement.children) {
      if (other !== pane && Math.abs(other.scrollLeft - pane.scrollLeft) >= 1) {
        other.scrollLeft = pane.scrollLeft;
        assigned.set(other, other.scrollLeft);
      }
    }
  }, true);
}

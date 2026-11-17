// Keeps the two panes of a side by side diff scrolled the same distance sideways. Scroll events don't bubble, so the
// body listens in the capture phase, which also covers panes that are drawn again when the diff is read again.

export function attach(body) {
  if (!body || body.dataset.scrollSync) {
    return;
  }

  body.dataset.scrollSync = 'true';
  let syncing = false;
  body.addEventListener('scroll', event => {
    const pane = event.target;
    if (syncing || !(pane instanceof HTMLElement) || !pane.classList.contains('git-diff-pane')) {
      return;
    }

    syncing = true;
    for (const other of pane.parentElement.children) {
      if (other !== pane) {
        other.scrollLeft = pane.scrollLeft;
      }
    }

    // The other pane's own scroll event is raised later, so it's ignored then.
    requestAnimationFrame(() => { syncing = false; });
  }, true);
}

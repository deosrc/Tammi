// Stops back and forward navigation in the web view, which a sideways swipe on a touchpad or a mouse's back and
// forward buttons would otherwise start, leaving the app's page. Tammi has no use for the browser's history, so it's
// switched off rather than only blocking those inputs, which the web view can handle before the page sees them.

// Every navigation replaces the current history entry, so there's never one to go back or forward to.
history.pushState = function (state, unused, url) {
  history.replaceState(state, unused, url);
};

// Where the web view has the Navigation API, a back or forward navigation that gets through is cancelled.
if (window.navigation) {
  window.navigation.addEventListener('navigate', event => {
    if (event.navigationType === 'traverse' && event.cancelable) {
      event.preventDefault();
    }
  });
}

// The back and forward buttons of a mouse.
for (const type of ['pointerdown', 'mousedown', 'mouseup', 'auxclick']) {
  window.addEventListener(type, event => {
    if (event.button === 3 || event.button === 4) {
      event.preventDefault();
      event.stopImmediatePropagation();
    }
  }, true);
}

// A sideways scroll that nothing under the pointer can take, including at the end of a scrollable area, is stopped
// so it can't become a swipe gesture.
function canScroll(element, deltaX) {
  const style = getComputedStyle(element);
  if (style.overflowX !== 'auto' && style.overflowX !== 'scroll') {
    return false;
  }

  return deltaX < 0
    ? element.scrollLeft > 0
    : element.scrollLeft + element.clientWidth < element.scrollWidth - 1;
}

window.addEventListener('wheel', event => {
  if (event.ctrlKey || Math.abs(event.deltaX) <= Math.abs(event.deltaY)) {
    return;
  }

  for (let element = event.target; element instanceof Element; element = element.parentElement) {
    if (canScroll(element, event.deltaX)) {
      return;
    }
  }

  event.preventDefault();
}, { passive: false });

import '@testing-library/jest-dom/vitest'

// jsdom implements most of the DOM but not layout-dependent APIs. MessageList scrolls to the
// newest message on mount, and without this stub React's effect throws inside the test.
if (typeof HTMLElement.prototype.scrollIntoView !== 'function') {
  HTMLElement.prototype.scrollIntoView = function scrollIntoView() {}
}

mergeInto(LibraryManager.library, {
  WorldSetGameplay: function (active) {
    if (typeof window !== "undefined" && window.__worldGameplay) {
      window.__worldGameplay(!!active);
    }
  }
});

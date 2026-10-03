mergeInto(LibraryManager.library, {
  HeistNotifyComplete: function (jsonPtr) {
    var json = UTF8ToString(jsonPtr);
    if (typeof window.onHeistComplete === "function") {
      window.onHeistComplete(json);
    }
  }
});

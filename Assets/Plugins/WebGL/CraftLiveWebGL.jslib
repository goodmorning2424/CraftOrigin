mergeInto(LibraryManager.library, {
  CraftLiveGetQueryParameter: function (keyPtr) {
    var key = UTF8ToString(keyPtr);
    var value = new URLSearchParams(window.location.search).get(key) || "";
    var size = lengthBytesUTF8(value) + 1;
    var buffer = _malloc(size);
    stringToUTF8(value, buffer, size);
    return buffer;
  },

  CraftLiveStartQrScanner: function (gameObjectPtr, timeoutMs) {
    var gameObjectName = UTF8ToString(gameObjectPtr);
    if (window.CraftLiveQrClose) {
      window.CraftLiveQrClose("", "", true);
    }

    var overlay = document.createElement("div");
    overlay.id = "craft-live-unity-qr-overlay";
    overlay.style.cssText =
      "position:fixed;inset:0;z-index:2147483647;background:#050505;" +
      "display:flex;align-items:center;justify-content:center;" +
      "flex-direction:column;gap:14px;padding:env(safe-area-inset-top) " +
      "env(safe-area-inset-right) env(safe-area-inset-bottom) " +
      "env(safe-area-inset-left);box-sizing:border-box;color:white;" +
      "font-family:-apple-system,BlinkMacSystemFont,sans-serif";

    var title = document.createElement("div");
    title.textContent = "素材のQRコードを枠の中に入れてください";
    title.style.cssText = "font-size:18px;text-align:center;padding:0 16px";

    var video = document.createElement("video");
    video.setAttribute("playsinline", "");
    video.setAttribute("autoplay", "");
    video.muted = true;
    video.style.cssText =
      "width:min(90vw,720px);height:min(68vh,880px);object-fit:cover;" +
      "background:#111;border:2px solid #78d5e3;border-radius:4px";

    var cancel = document.createElement("button");
    cancel.type = "button";
    cancel.textContent = "キャンセル";
    cancel.style.cssText =
      "font-size:18px;min-width:160px;min-height:48px;padding:10px 24px";

    overlay.appendChild(title);
    overlay.appendChild(video);
    overlay.appendChild(cancel);
    document.body.appendChild(overlay);

    var closed = false;
    var scanner = null;
    var stream = null;
    var timer = null;
    var animationFrame = 0;

    var close = function (method, message, silent) {
      if (closed) return;
      closed = true;
      if (timer) window.clearTimeout(timer);
      if (animationFrame) window.cancelAnimationFrame(animationFrame);
      if (scanner) {
        try { scanner.stop(); } catch (_) {}
        try { scanner.destroy(); } catch (_) {}
      }
      var activeStream = stream || video.srcObject;
      if (activeStream && activeStream.getTracks) {
        activeStream.getTracks().forEach(function (track) { track.stop(); });
      }
      overlay.remove();
      window.CraftLiveQrClose = null;
      if (!silent && method) {
        SendMessage(gameObjectName, method, message || "");
      }
    };
    window.CraftLiveQrClose = close;

    cancel.addEventListener("click", function () {
      close("OnQrScanCancelled", "", false);
    });

    timer = window.setTimeout(function () {
      close(
        "OnQrScanError",
        "QRコードの読み取り時間が終了しました。",
        false);
    }, Math.max(3000, timeoutMs || 12000));

    if (!window.isSecureContext ||
        !navigator.mediaDevices ||
        !navigator.mediaDevices.getUserMedia) {
      close(
        "OnQrScanError",
        "カメラを使用できません。HTTPSで開き、Safariのカメラ許可を確認してください。",
        false);
      return;
    }

    var startNativeDetector = function () {
      var detector = new BarcodeDetector({ formats: ["qr_code"] });
      return navigator.mediaDevices.getUserMedia({
        audio: false,
        video: {
          // The material QR is presented in front of the operator. Prefer
          // the iPad's inside/front camera instead of the rear camera.
          facingMode: { exact: "user" },
          width: { ideal: 1280 },
          height: { ideal: 720 }
        }
      }).then(function (cameraStream) {
        stream = cameraStream;
        video.srcObject = stream;
        return video.play();
      }).then(function () {
        var detect = function () {
          if (closed) return;
          detector.detect(video).then(function (codes) {
            if (codes && codes.length > 0) {
              close(
                "OnQrScanResult",
                codes[0].rawValue || "",
                false);
              return;
            }
            animationFrame = window.requestAnimationFrame(detect);
          }).catch(function () {
            animationFrame = window.requestAnimationFrame(detect);
          });
        };
        detect();
      });
    };

    var startQrScannerLibrary = function () {
      return import(
        "https://cdn.jsdelivr.net/npm/qr-scanner@1.4.2/qr-scanner.min.js"
      ).then(function (module) {
        if (closed) return;
        scanner = new module.default(video, function (result) {
          var value = typeof result === "string"
            ? result
            : (result && result.data) || "";
          close("OnQrScanResult", value, false);
        }, {
          preferredCamera: "user",
          maxScansPerSecond: 5,
          returnDetailedScanResult: true,
          highlightScanRegion: true,
          highlightCodeOutline: true
        });
        return scanner.start();
      });
    };

    var startPromise = "BarcodeDetector" in window
      ? startNativeDetector()
      : startQrScannerLibrary();
    startPromise.catch(function (error) {
      close(
        "OnQrScanError",
        "カメラを開始できません: " +
          String(error && error.message ? error.message : error),
        false);
    });
  },

  CraftLiveStopQrScanner: function () {
    if (window.CraftLiveQrClose) {
      window.CraftLiveQrClose("", "", true);
    }
  }
});

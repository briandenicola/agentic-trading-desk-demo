/* Shared interactions for every demo.
   Wire by data-attributes only (no inline handlers) so the markup stays clean. */
(function () {
  function toast(msg) {
    var t = document.createElement("div");
    t.className = "toast"; t.textContent = msg;
    document.body.appendChild(t);
    requestAnimationFrame(function () { t.classList.add("show"); });
    setTimeout(function () { t.classList.remove("show"); setTimeout(function () { t.remove(); }, 350); }, 2200);
  }

  document.addEventListener("DOMContentLoaded", function () {
    // Reveal an agent-output block, e.g. <button data-reveal="id">
    document.querySelectorAll("[data-reveal]").forEach(function (btn) {
      btn.addEventListener("click", function () {
        var el = document.getElementById(btn.getAttribute("data-reveal"));
        if (!el) return;
        el.classList.toggle("show");
        var on = el.classList.contains("show");
        btn.style.opacity = on ? "0.6" : "1";
        if (on) el.scrollIntoView({ behavior: "smooth", block: "nearest" });
      });
    });

    // Toggle contenteditable on a draft block: <button data-edit="id">
    document.querySelectorAll("[data-edit]").forEach(function (btn) {
      btn.addEventListener("click", function () {
        var el = document.getElementById(btn.getAttribute("data-edit"));
        if (!el) return;
        var editing = el.getAttribute("contenteditable") === "true";
        el.setAttribute("contenteditable", editing ? "false" : "true");
        btn.textContent = editing ? "Edit" : "Done editing";
        if (!editing) { el.focus(); toast("Editing — make your changes"); }
      });
    });

    // Confirmable action buttons: <button data-confirm="Sent!">
    document.querySelectorAll("[data-confirm]").forEach(function (btn) {
      btn.addEventListener("click", function () {
        if (btn.disabled) return;
        var label = btn.getAttribute("data-confirm") || "Done";
        var done = btn.getAttribute("data-done") || ("✓ " + label);
        btn.textContent = done; btn.disabled = true;
        toast(label);
        var after = btn.getAttribute("data-then");
        if (after) { var el = document.getElementById(after); if (el) el.classList.add("show"); }
      });
    });

    // Dismiss a card/alert: <button data-dismiss="id">
    document.querySelectorAll("[data-dismiss]").forEach(function (btn) {
      btn.addEventListener("click", function () {
        var el = document.getElementById(btn.getAttribute("data-dismiss"));
        if (el) { el.style.transition = "opacity .3s"; el.style.opacity = "0.35"; }
        btn.textContent = "Dismissed"; btn.disabled = true;
      });
    });
  });
})();

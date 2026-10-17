
"use strict";

document.addEventListener("DOMContentLoaded", () => {

    const menuButton =
        document.getElementById("menuButton");

    const archiveOverlay =
        document.getElementById("archiveOverlay");

    const closeButton =
        document.getElementById("closeArchive");

    if (!menuButton || !archiveOverlay || !closeButton) {
        return;
    }

    // Arhiivi avamine
    function openArchive() {
        archiveOverlay.classList.add("open");
        archiveOverlay.setAttribute("aria-hidden", "false");

        menuButton.setAttribute("aria-expanded", "true");

        document.body.style.overflow = "hidden";
        closeButton.focus();
    }

    // Arhiivi sulgemine
    function closeArchive() {
        archiveOverlay.classList.remove("open");
        archiveOverlay.setAttribute("aria-hidden", "true");

        menuButton.setAttribute("aria-expanded", "false");

        document.body.style.overflow = "";
        menuButton.focus();
    }

    // Burgermenüü
    menuButton.addEventListener(
        "click",
        openArchive
    );

    // Sulgemise nupp
    closeButton.addEventListener(
        "click",
        closeArchive
    );

    // Klõps väljaspool hüpikakent
    archiveOverlay.addEventListener("click", (event) => {
        if (event.target === archiveOverlay) {
            closeArchive();
        }
    });

    // Escape-klahv
    document.addEventListener("keydown", (event) => {
        if (
            event.key === "Escape" &&
            archiveOverlay.classList.contains("open")
        ) {
            closeArchive();
        }
    });
});

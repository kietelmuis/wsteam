// UI State Management
class AppState {
    constructor() {
        this.currentPage = "games";
        this.isDownloading = false;
        this.games = [];
    }

    setCurrentPage(page) {
        this.currentPage = page;
    }

    setDownloading(value) {
        this.isDownloading = value;
    }
}

// Main App Controller
class SteamDownloaderApp {
    constructor() {
        this.state = new AppState();
        this.setupEventListeners();
    }

    setupEventListeners() {
        // Navigation buttons
        document.querySelectorAll(".nav-button").forEach((btn) => {
            btn.addEventListener("click", (e) => this.handleNavClick(e));
        });

        // Add game button
        const addGameBtn = document.getElementById("add-game-btn");
        if (addGameBtn) {
            addGameBtn.addEventListener("click", () =>
                this.showDownloadModal(),
            );
        }

        // Modal close button
        const closeBtn = document.querySelector(".close-btn");
        if (closeBtn) {
            closeBtn.addEventListener("click", () => this.hideDownloadModal());
        }

        // Close modal on background click
        const modal = document.getElementById("progress-modal");
        if (modal) {
            modal.addEventListener("click", (e) => {
                if (e.target === modal) {
                    this.hideDownloadModal();
                }
            });
        }
    }

    handleNavClick(event) {
        const button = event.target.closest(".nav-button");
        if (!button) return;

        const page = button.getAttribute("data-page");

        // Update button states
        document.querySelectorAll(".nav-button").forEach((btn) => {
            btn.classList.remove("selected");
        });
        button.classList.add("selected");

        // Update page visibility
        document.querySelectorAll(".page").forEach((p) => {
            p.classList.remove("active");
        });

        const pageElement = document.getElementById(page);
        if (pageElement) {
            pageElement.classList.add("active");
        }

        // Update title
        const titles = {
            games: "Games",
            sources: "Sources",
            settings: "Settings",
        };
        document.getElementById("page-title").textContent =
            titles[page] || "Unknown";

        this.state.setCurrentPage(page);
    }

    showDownloadModal() {
        const modal = document.getElementById("progress-modal");
        if (modal) {
            modal.classList.remove("hidden");
        }

        // Focus on input if it exists
        const appIdInput = document.getElementById("app-id-input");
        if (appIdInput) {
            appIdInput.focus();
            appIdInput.addEventListener("keypress", (e) => {
                if (e.key === "Enter") {
                    appIdInput.disabled = true;
                    this.startDownload();
                }
            });
        }

        // Setup download button if it exists
        const startDownloadBtn = document.getElementById("start-download-btn");
        if (startDownloadBtn) {
            startDownloadBtn.addEventListener("click", () => {
                startDownloadBtn.disabled = true;
                this.startDownload();
            });
        }
    }

    hideDownloadModal() {
        const modal = document.getElementById("progress-modal");
        if (modal) {
            modal.classList.add("hidden");
        }
        this.resetProgress();

        // Clear input
        const appIdInput = document.getElementById("app-id-input");
        if (appIdInput) {
            appIdInput.value = "";
        }
    }

    startDownload() {
        const appIdInput = document.getElementById("app-id-input");
        if (!appIdInput) {
            // Fallback: use hardcoded app ID for testing
            const appId = "311210";
            this.downloadGame(appId);
            return;
        }

        const appId = appIdInput.value.trim();
        if (!appId) {
            alert("Please enter an App ID");
            return;
        }

        if (!/^\d+$/.test(appId)) {
            alert("App ID must be a number");
            return;
        }

        this.downloadGame(appId);
    }

    downloadGame(appId) {
        console.log("Starting download for App ID:", appId);

        this.state.setDownloading(true);

        // Show progress modal and container
        const modal = document.getElementById("progress-modal");
        if (modal) {
            modal.classList.remove("hidden");
        }

        const progressContainer = document.querySelector(".progress-container");
        if (progressContainer) {
            progressContainer.classList.remove("hidden");
        }

        try {
            console.log("Sending message to C# backend:", appId);
            window.external.sendMessage(appId);
        } catch (error) {
            console.error("Error sending message:", error);
            alert("Error starting download: " + error.message);
            this.state.setDownloading(false);
            this.hideDownloadModal();
        }

        window.external.receiveMessage((json) => {
            var data = JSON.parse(json);
            console.log("Received message from C# backend:", data);

            this.updateProgress(data.percentage, data.fileName, data.speed);
        });
    }

    updateProgress(percentage, filename, speed) {
        const progressFill = document.getElementById("progress-fill");
        if (progressFill && percentage !== null) {
            const percent = Math.min(100, percentage || 0);
            progressFill.style.width = percent + "%";
        }

        const filenameEl = document.getElementById("current-filename");
        if (filenameEl) {
            filenameEl.textContent = filename || "Downloading...";
        }

        const progressPercent = document.getElementById("progress-percent");
        if (progressPercent) {
            progressPercent.textContent = Math.round(percentage || 0) + "%";
        }

        const speedEl = document.getElementById("progress-speed");
        if (speedEl) {
            speedEl.textContent = speed;
        }
    }

    resetProgress() {
        const progressFill = document.getElementById("progress-fill");
        if (progressFill) {
            progressFill.style.width = "0%";
        }

        document.getElementById("progress-percent").textContent = "0%";
        document.getElementById("progress-speed").textContent = "0 MB/s";
        document.getElementById("current-filename").textContent = "-";
    }

    loadGames() {
        const gamesGrid = document.getElementById("games-grid");
        if (gamesGrid && this.state.games.length === 0) {
            gamesGrid.innerHTML =
                '<div class="game-placeholder">No games yet. Click "Add Game" to download.</div>';
        }
    }
}

// Initialize app when DOM is ready
document.addEventListener("DOMContentLoaded", () => {
    window.app = new SteamDownloaderApp();
    window.app.loadGames();
});

import { useState } from "react";
import { GamesPage } from "./components/GamesPage";
import { SourcesPage } from "./components/SourcesPage";
import { SettingsPage } from "./components/SettingsPage";

type Page = "games" | "sources" | "settings";

type Task = {
    id: number; // unique identifier
    name: string; // what the task is called
    progress: number; // e.g., 0–100, how far along it is
    speed: string; // optional extra info, like "fast" or "slow"
};

type DownloadMessage = {
    appId: number;
    appName: string;
    speed: string;
    percentage: number;
};

const NAV_ITEMS: { id: Page; label: string; icon: React.ReactNode }[] = [
    {
        id: "games",
        label: "Library",
        icon: (
            <svg
                width="16"
                height="16"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.8"
            >
                <rect x="2" y="6" width="20" height="12" rx="2" />
                <path
                    d="M12 12h.01M7 12h.01M17 12h.01"
                    strokeWidth="2.5"
                    strokeLinecap="round"
                />
            </svg>
        ),
    },
    {
        id: "sources",
        label: "Sources",
        icon: (
            <svg
                width="16"
                height="16"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.8"
            >
                <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                <polyline points="7 10 12 15 17 10" />
                <line x1="12" y1="15" x2="12" y2="3" />
            </svg>
        ),
    },
    {
        id: "settings",
        label: "Settings",
        icon: (
            <svg
                width="16"
                height="16"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.8"
            >
                <circle cx="12" cy="12" r="3" />
                <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
            </svg>
        ),
    },
];

const PAGE_TITLES: Record<Page, { title: string; subtitle: string }> = {
    games: { title: "Library", subtitle: "Your installed games" },
    sources: { title: "Sources", subtitle: "Manage JSON game repositories" },
    settings: { title: "Settings", subtitle: "Configure download preferences" },
};

export default function App() {
    const [page, setPage] = useState<Page>("games");
    const [downloading, setDownloading] = useState<Map<number, Task>>(
        new Map(),
    );

    window.external.receiveMessage((json: string) => {
        const { appId, appName, speed, percentage } = JSON.parse(
            json,
        ) as DownloadMessage;
        setDownloading((prev) => {
            const updated = new Map(prev);
            updated.set(appId, {
                id: appId,
                name: appName,
                progress: percentage,
                speed: speed,
            });
            return updated;
        });
    });

    const { title, subtitle } = PAGE_TITLES[page];

    return (
        <div className="w-screen h-screen bg-[#0e1016] flex overflow-hidden select-none">
            {/* Sidebar */}
            <aside className="w-52 shrink-0 bg-[#13151b] border-r border-[#2a2d35] flex flex-col">
                {/* Logo */}
                <div className="px-5 py-5 border-b border-[#2a2d35]">
                    <div className="flex items-center gap-2.5">
                        <div className="w-7 h-7 bg-[#4a9eff] rounded-md flex items-center justify-center">
                            <svg
                                width="14"
                                height="14"
                                viewBox="0 0 24 24"
                                fill="white"
                            >
                                <path
                                    d="M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5"
                                    stroke="white"
                                    strokeWidth="2"
                                    fill="none"
                                    strokeLinejoin="round"
                                />
                            </svg>
                        </div>
                        <span
                            className="text-white text-base tracking-tight"
                            style={{
                                fontWeight: 600,
                                letterSpacing: "-0.02em",
                            }}
                        >
                            wsteam
                        </span>
                    </div>
                </div>

                {/* Nav */}
                <nav className="flex-1 py-3 px-2">
                    <p className="text-[#3a3d45] text-[10px] uppercase tracking-widest px-3 mb-2">
                        Navigation
                    </p>
                    {NAV_ITEMS.map((item) => (
                        <button
                            key={item.id}
                            onClick={() => setPage(item.id)}
                            className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-md mb-0.5 transition-all duration-150 text-left ${
                                page === item.id
                                    ? "bg-[#4a9eff]/15 text-[#4a9eff]"
                                    : "text-[#5a5f70] hover:text-[#c8cad4] hover:bg-[#1a1c23]"
                            }`}
                        >
                            <span
                                className={
                                    page === item.id ? "text-[#4a9eff]" : ""
                                }
                            >
                                {item.icon}
                            </span>
                            <span className="text-sm">{item.label}</span>
                            {page === item.id && (
                                <span className="ml-auto w-1 h-4 bg-[#4a9eff] rounded-full" />
                            )}
                        </button>
                    ))}
                </nav>

                {/* Active downloads */}
                {downloading.size > 0 && (
                    <div className="border-t border-[#2a2d35] px-3 py-3">
                        <p className="text-[#3a3d45] text-[10px] uppercase tracking-widest mb-2 px-1">
                            Downloading
                        </p>
                        {Array.from(downloading.values()).map((dl) => (
                            <div key={dl.id} className="px-1 mb-2">
                                <p className="text-[#8a8f9e] text-xs truncate">
                                    {dl.name}
                                </p>
                                <div className="flex items-center gap-2 mt-1.5">
                                    <div className="flex-1 h-1 bg-[#2a2d35] rounded-full overflow-hidden">
                                        <div
                                            className="h-full bg-[#4a9eff] rounded-full transition-all"
                                            style={{ width: `${dl.progress}%` }}
                                        />
                                    </div>
                                    <span className="text-[#5a5f70] text-[10px] shrink-0">
                                        {dl.progress}%
                                    </span>
                                </div>
                                <p className="text-[#3a3d45] text-[10px] mt-0.5">
                                    {dl.speed}
                                </p>
                            </div>
                        ))}
                    </div>
                )}

                {/* User area */}
                <div className="border-t border-[#2a2d35] px-3 py-3">
                    <div className="flex items-center gap-2.5 px-1">
                        <div
                            className="w-7 h-7 rounded-full bg-gradient-to-br from-[#4a9eff] to-[#a855f7] flex items-center justify-center text-white text-xs shrink-0"
                            style={{ fontWeight: 600 }}
                        >
                            U
                        </div>
                        <div className="flex-1 min-w-0">
                            <p className="text-[#c8cad4] text-xs truncate">
                                user
                            </p>
                            <p className="text-[#3a3d45] text-[10px]">Online</p>
                        </div>
                        <div className="w-1.5 h-1.5 rounded-full bg-[#3ddc84] shrink-0" />
                    </div>
                </div>
            </aside>

            {/* Main content */}
            <main className="flex-1 flex flex-col min-w-0 relative">
                {/* Top bar */}
                <div className="h-14 shrink-0 border-b border-[#2a2d35] bg-[#13151b] flex items-center px-6 gap-4">
                    <div>
                        <h1
                            className="text-[#c8cad4]"
                            style={{ lineHeight: 1.2 }}
                        >
                            {title}
                        </h1>
                        <p className="text-[#5a5f70] text-xs">{subtitle}</p>
                    </div>
                </div>

                {/* Page content */}
                <div className="flex-1 overflow-hidden flex flex-col relative">
                    {page === "games" && <GamesPage />}
                    {page === "sources" && <SourcesPage />}
                    {page === "settings" && <SettingsPage />}
                </div>
            </main>
        </div>
    );
}

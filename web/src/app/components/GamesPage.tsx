import { useState } from "react";

const GAMES = [
  {
    id: 1,
    title: "Iron Vanguard",
    genre: "Action / Adventure",
    size: "24.8 GB",
    version: "1.4.2",
    lastPlayed: "Today",
    status: "ready",
    cover: "https://images.unsplash.com/photo-1740390133235-e82eba2c040a?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhY3Rpb24lMjBhZHZlbnR1cmUlMjBnYW1lJTIwY292ZXIlMjBhcnR8ZW58MXx8fHwxNzczNjE4MDQ1fDA&ixlib=rb-4.1.0&q=80&w=1080",
  },
  {
    id: 2,
    title: "Nebula Protocol",
    genre: "Sci-Fi / Shooter",
    size: "38.2 GB",
    version: "2.1.0",
    lastPlayed: "Yesterday",
    status: "ready",
    cover: "https://images.unsplash.com/photo-1647323968696-0ea09525407c?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxzY2ktZmklMjBzcGFjZSUyMGdhbWUlMjBjb3ZlcnxlbnwxfHx8fDE3NzM2MTgwNDZ8MA&ixlib=rb-4.1.0&q=80&w=1080",
  },
  {
    id: 3,
    title: "Realm of Eternity",
    genre: "Fantasy / RPG",
    size: "61.5 GB",
    version: "3.7.1",
    lastPlayed: "3 days ago",
    status: "ready",
    cover: "https://images.unsplash.com/photo-1742893989685-afbfcf3b18e0?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxmYW50YXN5JTIwcnBnJTIwZ2FtZSUyMGFydHdvcmt8ZW58MXx8fHwxNzczNjE4MDQ2fDA&ixlib=rb-4.1.0&q=80&w=1080",
  },
  {
    id: 4,
    title: "Velocity Rush",
    genre: "Racing / Arcade",
    size: "12.3 GB",
    version: "1.0.5",
    lastPlayed: "Last week",
    status: "update",
    cover: "https://images.unsplash.com/photo-1674666735108-e3adc95ead30?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxyYWNpbmclMjBjYXIlMjBnYW1lJTIwbmVvbnxlbnwxfHx8fDE3NzM2MTgwNDZ8MA&ixlib=rb-4.1.0&q=80&w=1080",
  },
  {
    id: 5,
    title: "Darkwood Chronicles",
    genre: "Horror / Survival",
    size: "18.9 GB",
    version: "2.3.4",
    lastPlayed: "2 weeks ago",
    status: "ready",
    cover: "https://images.unsplash.com/photo-1597839219216-a773cb2473e4?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxob3Jyb3IlMjBkYXJrJTIwZm9yZXN0JTIwZ2FtZXxlbnwxfHx8fDE3NzM2MTgwNDd8MA&ixlib=rb-4.1.0&q=80&w=1080",
  },
  {
    id: 6,
    title: "Conquest: Zero Hour",
    genre: "Strategy / War",
    size: "9.7 GB",
    version: "1.2.0",
    lastPlayed: "Never",
    status: "update",
    cover: "https://images.unsplash.com/photo-1761274441884-357dad8f28ab?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxzdHJhdGVneSUyMHdhciUyMGdhbWUlMjBiYXR0bGVmaWVsZHxlbnwxfHx8fDE3NzM2MTgwNDd8MA&ixlib=rb-4.1.0&q=80&w=1080",
  },
];

type ViewMode = "grid" | "list";

export function GamesPage() {
  const [search, setSearch] = useState("");
  const [viewMode, setViewMode] = useState<ViewMode>("grid");
  const [filter, setFilter] = useState<"all" | "ready" | "update">("all");
  const [selectedGame, setSelectedGame] = useState<number | null>(null);

  const filtered = GAMES.filter((g) => {
    const matchSearch =
      g.title.toLowerCase().includes(search.toLowerCase()) ||
      g.genre.toLowerCase().includes(search.toLowerCase());
    const matchFilter = filter === "all" || g.status === filter;
    return matchSearch && matchFilter;
  });

  return (
    <div className="flex flex-col h-full">
      {/* Toolbar */}
      <div className="flex items-center gap-3 px-6 py-4 border-b border-[#2a2d35]">
        {/* Search */}
        <div className="relative flex-1 max-w-sm">
          <svg
            className="absolute left-3 top-1/2 -translate-y-1/2 text-[#5a5f70]"
            width="14"
            height="14"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
          >
            <circle cx="11" cy="11" r="8" />
            <path d="m21 21-4.35-4.35" />
          </svg>
          <input
            type="text"
            placeholder="Search games..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full bg-[#1a1c23] text-[#c8cad4] placeholder-[#5a5f70] pl-9 pr-3 py-2 rounded-md border border-[#2a2d35] outline-none focus:border-[#4a9eff] transition-colors text-sm"
          />
        </div>

        {/* Filters */}
        <div className="flex items-center gap-1 bg-[#1a1c23] rounded-md border border-[#2a2d35] p-1">
          {(["all", "ready", "update"] as const).map((f) => (
            <button
              key={f}
              onClick={() => setFilter(f)}
              className={`px-3 py-1 rounded text-xs capitalize transition-colors ${
                filter === f
                  ? "bg-[#4a9eff] text-white"
                  : "text-[#8a8f9e] hover:text-[#c8cad4]"
              }`}
            >
              {f === "all" ? "All Games" : f === "ready" ? "Ready" : "Update Available"}
            </button>
          ))}
        </div>

        <div className="ml-auto flex items-center gap-1 bg-[#1a1c23] rounded-md border border-[#2a2d35] p-1">
          <button
            onClick={() => setViewMode("grid")}
            className={`p-1.5 rounded transition-colors ${
              viewMode === "grid" ? "bg-[#2a2d35] text-[#4a9eff]" : "text-[#5a5f70] hover:text-[#c8cad4]"
            }`}
            title="Grid view"
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
              <rect x="3" y="3" width="7" height="7" rx="1" />
              <rect x="14" y="3" width="7" height="7" rx="1" />
              <rect x="3" y="14" width="7" height="7" rx="1" />
              <rect x="14" y="14" width="7" height="7" rx="1" />
            </svg>
          </button>
          <button
            onClick={() => setViewMode("list")}
            className={`p-1.5 rounded transition-colors ${
              viewMode === "list" ? "bg-[#2a2d35] text-[#4a9eff]" : "text-[#5a5f70] hover:text-[#c8cad4]"
            }`}
            title="List view"
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <line x1="8" y1="6" x2="21" y2="6" />
              <line x1="8" y1="12" x2="21" y2="12" />
              <line x1="8" y1="18" x2="21" y2="18" />
              <line x1="3" y1="6" x2="3.01" y2="6" />
              <line x1="3" y1="12" x2="3.01" y2="12" />
              <line x1="3" y1="18" x2="3.01" y2="18" />
            </svg>
          </button>
        </div>

        <span className="text-[#5a5f70] text-xs">{filtered.length} game{filtered.length !== 1 ? "s" : ""}</span>
      </div>

      {/* Games */}
      <div className="flex-1 overflow-y-auto p-6">
        {filtered.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-full text-center">
            <svg className="text-[#2a2d35] mb-4" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1">
              <rect x="2" y="6" width="20" height="12" rx="2" />
              <path d="M12 12h.01" />
              <path d="M7 12h.01" />
              <path d="M17 12h.01" />
            </svg>
            <p className="text-[#5a5f70] text-sm">No games found</p>
          </div>
        ) : viewMode === "grid" ? (
          <div className="grid grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
            {filtered.map((game) => (
              <div
                key={game.id}
                onClick={() => setSelectedGame(selectedGame === game.id ? null : game.id)}
                className={`group relative rounded-lg overflow-hidden border cursor-pointer transition-all duration-200 ${
                  selectedGame === game.id
                    ? "border-[#4a9eff] shadow-lg shadow-[#4a9eff]/20"
                    : "border-[#2a2d35] hover:border-[#3a3d45]"
                }`}
              >
                <div className="aspect-[3/4] relative overflow-hidden">
                  <img
                    src={game.cover}
                    alt={game.title}
                    className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                  />
                  <div className="absolute inset-0 bg-gradient-to-t from-[#0e1016] via-transparent to-transparent" />
                  {game.status === "update" && (
                    <div className="absolute top-2 right-2 bg-[#f5a623] text-[#0e1016] text-[10px] px-1.5 py-0.5 rounded font-medium">
                      UPDATE
                    </div>
                  )}
                </div>
                <div className="absolute bottom-0 left-0 right-0 p-3">
                  <p className="text-white text-sm truncate">{game.title}</p>
                  <p className="text-[#8a8f9e] text-xs truncate">{game.genre}</p>
                </div>
                {/* Hover overlay */}
                <div className="absolute inset-0 bg-[#4a9eff]/0 group-hover:bg-[#4a9eff]/5 transition-colors duration-200 flex items-center justify-center opacity-0 group-hover:opacity-100">
                  <div className="bg-[#4a9eff] text-white rounded-full p-3 shadow-lg transform scale-90 group-hover:scale-100 transition-transform duration-200">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
                      <polygon points="5 3 19 12 5 21 5 3" />
                    </svg>
                  </div>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div className="flex flex-col gap-1">
            {filtered.map((game) => (
              <div
                key={game.id}
                onClick={() => setSelectedGame(selectedGame === game.id ? null : game.id)}
                className={`flex items-center gap-4 p-3 rounded-lg border cursor-pointer transition-all duration-150 ${
                  selectedGame === game.id
                    ? "border-[#4a9eff] bg-[#4a9eff]/5"
                    : "border-transparent hover:border-[#2a2d35] hover:bg-[#1a1c23]"
                }`}
              >
                <img
                  src={game.cover}
                  alt={game.title}
                  className="w-12 h-16 object-cover rounded"
                />
                <div className="flex-1 min-w-0">
                  <p className="text-[#c8cad4] text-sm">{game.title}</p>
                  <p className="text-[#5a5f70] text-xs">{game.genre}</p>
                </div>
                <div className="text-right shrink-0">
                  <p className="text-[#8a8f9e] text-xs">{game.size}</p>
                  <p className="text-[#5a5f70] text-xs">v{game.version}</p>
                </div>
                <div className="text-right shrink-0 w-24">
                  <p className="text-[#5a5f70] text-xs">{game.lastPlayed}</p>
                </div>
                <div className="shrink-0">
                  {game.status === "update" ? (
                    <button className="bg-[#f5a623] text-[#0e1016] text-xs px-3 py-1.5 rounded hover:bg-[#e09510] transition-colors">
                      Update
                    </button>
                  ) : (
                    <button className="bg-[#4a9eff] text-white text-xs px-3 py-1.5 rounded hover:bg-[#3a8eef] transition-colors">
                      Play
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Selected game detail panel */}
      {selectedGame !== null && (
        <div className="border-t border-[#2a2d35] bg-[#13151b] p-4 flex items-center gap-5">
          {(() => {
            const game = GAMES.find((g) => g.id === selectedGame)!;
            return (
              <>
                <img src={game.cover} alt={game.title} className="w-16 h-20 object-cover rounded" />
                <div className="flex-1">
                  <h3 className="text-[#c8cad4]">{game.title}</h3>
                  <p className="text-[#5a5f70] text-xs mt-0.5">{game.genre} · {game.size} · v{game.version}</p>
                  <p className="text-[#5a5f70] text-xs mt-0.5">Last played: {game.lastPlayed}</p>
                </div>
                <div className="flex gap-2">
                  <button className="bg-[#1a1c23] text-[#8a8f9e] border border-[#2a2d35] text-sm px-4 py-2 rounded hover:border-[#3a3d45] hover:text-[#c8cad4] transition-colors">
                    Properties
                  </button>
                  {game.status === "update" ? (
                    <button className="bg-[#f5a623] text-[#0e1016] text-sm px-4 py-2 rounded hover:bg-[#e09510] transition-colors">
                      Update Game
                    </button>
                  ) : (
                    <button className="bg-[#4a9eff] text-white text-sm px-6 py-2 rounded hover:bg-[#3a8eef] transition-colors">
                      ▶ Play
                    </button>
                  )}
                </div>
              </>
            );
          })()}
        </div>
      )}
    </div>
  );
}

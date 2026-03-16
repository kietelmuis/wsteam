import { useState } from "react";

type SourceStatus = "connected" | "error" | "pending";

interface Source {
  id: number;
  name: string;
  url: string;
  status: SourceStatus;
  gameCount: number;
  lastSync: string;
  description: string;
}

const INITIAL_SOURCES: Source[] = [
  {
    id: 1,
    name: "Main Repository",
    url: "https://cdn.wsteam.io/sources/main.json",
    status: "connected",
    gameCount: 1240,
    lastSync: "2 min ago",
    description: "Official wsteam main game repository",
  },
  {
    id: 2,
    name: "Indie Vault",
    url: "https://indievault.net/api/wsteam.json",
    status: "connected",
    gameCount: 387,
    lastSync: "15 min ago",
    description: "Curated indie game collection",
  },
  {
    id: 3,
    name: "RetroLib",
    url: "https://retrolib.org/catalog.json",
    status: "error",
    gameCount: 0,
    lastSync: "Failed",
    description: "Classic and retro game archive",
  },
];

const STATUS_CONFIG: Record<SourceStatus, { label: string; dot: string; text: string }> = {
  connected: { label: "Connected", dot: "bg-[#3ddc84]", text: "text-[#3ddc84]" },
  error: { label: "Error", dot: "bg-[#ff5f5f]", text: "text-[#ff5f5f]" },
  pending: { label: "Connecting...", dot: "bg-[#f5a623]", text: "text-[#f5a623]" },
};

export function SourcesPage() {
  const [sources, setSources] = useState<Source[]>(INITIAL_SOURCES);
  const [showAdd, setShowAdd] = useState(false);
  const [newUrl, setNewUrl] = useState("");
  const [newName, setNewName] = useState("");
  const [newDesc, setNewDesc] = useState("");
  const [editId, setEditId] = useState<number | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState<number | null>(null);
  const [syncing, setSyncing] = useState<number | null>(null);

  const handleAdd = () => {
    if (!newUrl.trim() || !newName.trim()) return;
    const newSource: Source = {
      id: Date.now(),
      name: newName.trim(),
      url: newUrl.trim(),
      status: "pending",
      gameCount: 0,
      lastSync: "Never",
      description: newDesc.trim() || "Custom source",
    };
    setSources((prev) => [...prev, newSource]);
    setTimeout(() => {
      setSources((prev) =>
        prev.map((s) =>
          s.id === newSource.id ? { ...s, status: "connected", gameCount: Math.floor(Math.random() * 500) + 50, lastSync: "Just now" } : s
        )
      );
    }, 1500);
    setNewUrl("");
    setNewName("");
    setNewDesc("");
    setShowAdd(false);
  };

  const handleDelete = (id: number) => {
    setSources((prev) => prev.filter((s) => s.id !== id));
    setDeleteConfirm(null);
  };

  const handleSync = (id: number) => {
    setSyncing(id);
    setSources((prev) => prev.map((s) => (s.id === id ? { ...s, status: "pending" } : s)));
    setTimeout(() => {
      setSources((prev) =>
        prev.map((s) =>
          s.id === id
            ? { ...s, status: Math.random() > 0.2 ? "connected" : "error", lastSync: Math.random() > 0.2 ? "Just now" : "Failed" }
            : s
        )
      );
      setSyncing(null);
    }, 1800);
  };

  const handleSyncAll = () => {
    sources.forEach((s) => handleSync(s.id));
  };

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="flex items-center justify-between px-6 py-4 border-b border-[#2a2d35]">
        <div>
          <p className="text-[#5a5f70] text-xs mt-0.5">
            {sources.filter((s) => s.status === "connected").length} of {sources.length} sources connected ·{" "}
            {sources.filter((s) => s.status === "connected").reduce((acc, s) => acc + s.gameCount, 0).toLocaleString()} total games
          </p>
        </div>
        <div className="flex gap-2">
          <button
            onClick={handleSyncAll}
            className="flex items-center gap-2 bg-[#1a1c23] border border-[#2a2d35] text-[#8a8f9e] text-sm px-3 py-2 rounded hover:border-[#3a3d45] hover:text-[#c8cad4] transition-colors"
          >
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M23 4v6h-6" />
              <path d="M1 20v-6h6" />
              <path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10" />
              <path d="M20.49 15a9 9 0 0 1-14.85 3.36L1 14" />
            </svg>
            Sync All
          </button>
          <button
            onClick={() => setShowAdd(true)}
            className="flex items-center gap-2 bg-[#4a9eff] text-white text-sm px-3 py-2 rounded hover:bg-[#3a8eef] transition-colors"
          >
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
              <line x1="12" y1="5" x2="12" y2="19" />
              <line x1="5" y1="12" x2="19" y2="12" />
            </svg>
            Add Source
          </button>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto p-6 flex flex-col gap-3">
        {sources.map((source) => {
          const sc = STATUS_CONFIG[source.status];
          const isEditing = editId === source.id;
          return (
            <div
              key={source.id}
              className="bg-[#1a1c23] border border-[#2a2d35] rounded-lg overflow-hidden"
            >
              {isEditing ? (
                <div className="p-4 flex flex-col gap-3">
                  <input
                    type="text"
                    defaultValue={source.name}
                    className="bg-[#13151b] border border-[#2a2d35] text-[#c8cad4] px-3 py-2 rounded text-sm outline-none focus:border-[#4a9eff] transition-colors"
                    placeholder="Source name"
                    id={`edit-name-${source.id}`}
                  />
                  <input
                    type="text"
                    defaultValue={source.url}
                    className="bg-[#13151b] border border-[#2a2d35] text-[#c8cad4] px-3 py-2 rounded text-sm outline-none focus:border-[#4a9eff] transition-colors font-mono"
                    placeholder="JSON URL"
                    id={`edit-url-${source.id}`}
                  />
                  <input
                    type="text"
                    defaultValue={source.description}
                    className="bg-[#13151b] border border-[#2a2d35] text-[#c8cad4] px-3 py-2 rounded text-sm outline-none focus:border-[#4a9eff] transition-colors"
                    placeholder="Description"
                    id={`edit-desc-${source.id}`}
                  />
                  <div className="flex gap-2 justify-end">
                    <button onClick={() => setEditId(null)} className="text-sm text-[#8a8f9e] px-3 py-1.5 rounded hover:text-[#c8cad4] transition-colors">
                      Cancel
                    </button>
                    <button
                      onClick={() => {
                        const name = (document.getElementById(`edit-name-${source.id}`) as HTMLInputElement).value;
                        const url = (document.getElementById(`edit-url-${source.id}`) as HTMLInputElement).value;
                        const desc = (document.getElementById(`edit-desc-${source.id}`) as HTMLInputElement).value;
                        setSources((prev) => prev.map((s) => s.id === source.id ? { ...s, name, url, description: desc } : s));
                        setEditId(null);
                      }}
                      className="bg-[#4a9eff] text-white text-sm px-3 py-1.5 rounded hover:bg-[#3a8eef] transition-colors"
                    >
                      Save
                    </button>
                  </div>
                </div>
              ) : (
                <div className="p-4">
                  <div className="flex items-start justify-between gap-4">
                    <div className="flex items-start gap-3 flex-1 min-w-0">
                      <div className="mt-1 p-2 bg-[#13151b] rounded-md border border-[#2a2d35] shrink-0">
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#4a9eff" strokeWidth="1.5">
                          <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                          <polyline points="7 10 12 15 17 10" />
                          <line x1="12" y1="15" x2="12" y2="3" />
                        </svg>
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2">
                          <span className="text-[#c8cad4] text-sm">{source.name}</span>
                          <span className={`flex items-center gap-1 text-xs ${sc.text}`}>
                            <span className={`inline-block w-1.5 h-1.5 rounded-full ${sc.dot} ${source.status === "pending" ? "animate-pulse" : ""}`} />
                            {sc.label}
                          </span>
                        </div>
                        <p className="text-[#5a5f70] text-xs mt-0.5 font-mono truncate">{source.url}</p>
                        <p className="text-[#5a5f70] text-xs mt-1">{source.description}</p>
                      </div>
                    </div>
                    <div className="flex items-center gap-1 shrink-0">
                      <button
                        onClick={() => handleSync(source.id)}
                        disabled={syncing === source.id}
                        className="p-1.5 rounded text-[#5a5f70] hover:text-[#4a9eff] hover:bg-[#4a9eff]/10 transition-colors disabled:opacity-50"
                        title="Sync"
                      >
                        <svg
                          className={syncing === source.id ? "animate-spin" : ""}
                          width="14"
                          height="14"
                          viewBox="0 0 24 24"
                          fill="none"
                          stroke="currentColor"
                          strokeWidth="2"
                        >
                          <path d="M23 4v6h-6" /><path d="M1 20v-6h6" />
                          <path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10" />
                          <path d="M20.49 15a9 9 0 0 1-14.85 3.36L1 14" />
                        </svg>
                      </button>
                      <button
                        onClick={() => setEditId(source.id)}
                        className="p-1.5 rounded text-[#5a5f70] hover:text-[#c8cad4] hover:bg-[#2a2d35] transition-colors"
                        title="Edit"
                      >
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                          <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                          <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
                        </svg>
                      </button>
                      {deleteConfirm === source.id ? (
                        <div className="flex items-center gap-1">
                          <span className="text-[#ff5f5f] text-xs">Delete?</span>
                          <button onClick={() => handleDelete(source.id)} className="text-xs text-[#ff5f5f] px-2 py-0.5 rounded border border-[#ff5f5f]/30 hover:bg-[#ff5f5f]/10 transition-colors">Yes</button>
                          <button onClick={() => setDeleteConfirm(null)} className="text-xs text-[#8a8f9e] px-2 py-0.5 rounded border border-[#2a2d35] hover:bg-[#2a2d35] transition-colors">No</button>
                        </div>
                      ) : (
                        <button
                          onClick={() => setDeleteConfirm(source.id)}
                          className="p-1.5 rounded text-[#5a5f70] hover:text-[#ff5f5f] hover:bg-[#ff5f5f]/10 transition-colors"
                          title="Delete"
                        >
                          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                            <polyline points="3 6 5 6 21 6" />
                            <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2" />
                          </svg>
                        </button>
                      )}
                    </div>
                  </div>
                  {source.status !== "pending" && (
                    <div className="flex items-center gap-4 mt-3 pt-3 border-t border-[#2a2d35]">
                      <span className="text-[#5a5f70] text-xs">
                        <span className="text-[#8a8f9e]">{source.gameCount.toLocaleString()}</span> games
                      </span>
                      <span className="text-[#5a5f70] text-xs">
                        Last synced: <span className="text-[#8a8f9e]">{source.lastSync}</span>
                      </span>
                    </div>
                  )}
                </div>
              )}
            </div>
          );
        })}

        {sources.length === 0 && (
          <div className="flex flex-col items-center justify-center flex-1 text-center py-20">
            <svg className="text-[#2a2d35] mb-4" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
              <polyline points="7 10 12 15 17 10" />
              <line x1="12" y1="15" x2="12" y2="3" />
            </svg>
            <p className="text-[#5a5f70] text-sm">No sources configured</p>
            <p className="text-[#3a3d45] text-xs mt-1">Add a JSON source to start browsing games</p>
          </div>
        )}
      </div>

      {/* Add Source Modal */}
      {showAdd && (
        <div className="absolute inset-0 bg-[#0e1016]/80 flex items-center justify-center z-50" onClick={() => setShowAdd(false)}>
          <div className="bg-[#1a1c23] border border-[#2a2d35] rounded-xl p-6 w-full max-w-md shadow-2xl" onClick={(e) => e.stopPropagation()}>
            <div className="flex items-center justify-between mb-5">
              <h2 className="text-[#c8cad4]">Add New Source</h2>
              <button onClick={() => setShowAdd(false)} className="text-[#5a5f70] hover:text-[#c8cad4] transition-colors">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />
                </svg>
              </button>
            </div>
            <div className="flex flex-col gap-3">
              <div>
                <label className="text-[#8a8f9e] text-xs block mb-1">Source Name</label>
                <input
                  type="text"
                  value={newName}
                  onChange={(e) => setNewName(e.target.value)}
                  placeholder="My Game Source"
                  className="w-full bg-[#13151b] border border-[#2a2d35] text-[#c8cad4] px-3 py-2 rounded text-sm outline-none focus:border-[#4a9eff] transition-colors"
                />
              </div>
              <div>
                <label className="text-[#8a8f9e] text-xs block mb-1">JSON URL</label>
                <input
                  type="url"
                  value={newUrl}
                  onChange={(e) => setNewUrl(e.target.value)}
                  placeholder="https://example.com/source.json"
                  className="w-full bg-[#13151b] border border-[#2a2d35] text-[#c8cad4] px-3 py-2 rounded text-sm outline-none focus:border-[#4a9eff] transition-colors font-mono"
                />
              </div>
              <div>
                <label className="text-[#8a8f9e] text-xs block mb-1">Description <span className="text-[#3a3d45]">(optional)</span></label>
                <input
                  type="text"
                  value={newDesc}
                  onChange={(e) => setNewDesc(e.target.value)}
                  placeholder="Describe this source..."
                  className="w-full bg-[#13151b] border border-[#2a2d35] text-[#c8cad4] px-3 py-2 rounded text-sm outline-none focus:border-[#4a9eff] transition-colors"
                />
              </div>
              <div className="bg-[#13151b] border border-[#2a2d35] rounded-lg p-3 mt-1">
                <p className="text-[#5a5f70] text-xs leading-relaxed">
                  The JSON file must follow the wsteam source schema. It should contain a list of game entries with metadata and download links.
                </p>
              </div>
              <div className="flex gap-2 justify-end mt-2">
                <button onClick={() => setShowAdd(false)} className="text-sm text-[#8a8f9e] px-4 py-2 rounded border border-[#2a2d35] hover:border-[#3a3d45] hover:text-[#c8cad4] transition-colors">
                  Cancel
                </button>
                <button
                  onClick={handleAdd}
                  disabled={!newUrl.trim() || !newName.trim()}
                  className="bg-[#4a9eff] text-white text-sm px-4 py-2 rounded hover:bg-[#3a8eef] transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                >
                  Add Source
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

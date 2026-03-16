import { useState } from "react";

interface ToggleProps {
  checked: boolean;
  onChange: (v: boolean) => void;
  disabled?: boolean;
}

function Toggle({ checked, onChange, disabled }: ToggleProps) {
  return (
    <button
      role="switch"
      aria-checked={checked}
      onClick={() => !disabled && onChange(!checked)}
      className={`relative inline-flex w-9 h-5 rounded-full transition-colors duration-200 focus:outline-none ${
        checked ? "bg-[#4a9eff]" : "bg-[#2a2d35]"
      } ${disabled ? "opacity-40 cursor-not-allowed" : "cursor-pointer"}`}
    >
      <span
        className={`inline-block w-3.5 h-3.5 rounded-full bg-white shadow transform transition-transform duration-200 mt-[3px] ${
          checked ? "translate-x-4" : "translate-x-0.5"
        }`}
      />
    </button>
  );
}

interface SliderProps {
  value: number;
  min: number;
  max: number;
  step?: number;
  onChange: (v: number) => void;
  disabled?: boolean;
}

function Slider({ value, min, max, step = 1, onChange, disabled }: SliderProps) {
  const pct = ((value - min) / (max - min)) * 100;
  return (
    <div className="relative w-full h-5 flex items-center">
      <input
        type="range"
        min={min}
        max={max}
        step={step}
        value={value}
        disabled={disabled}
        onChange={(e) => onChange(Number(e.target.value))}
        className="w-full appearance-none h-1.5 rounded-full outline-none cursor-pointer disabled:cursor-not-allowed disabled:opacity-40"
        style={{
          background: `linear-gradient(to right, #4a9eff ${pct}%, #2a2d35 ${pct}%)`,
        }}
      />
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <div className="flex items-center gap-3 mb-3">
        <h2 className="text-[#8a8f9e] text-xs uppercase tracking-widest">{title}</h2>
        <div className="flex-1 h-px bg-[#2a2d35]" />
      </div>
      <div className="bg-[#1a1c23] rounded-lg border border-[#2a2d35] overflow-hidden">
        {children}
      </div>
    </div>
  );
}

function Row({
  label,
  description,
  children,
  last,
}: {
  label: string;
  description?: string;
  children: React.ReactNode;
  last?: boolean;
}) {
  return (
    <div className={`flex items-center justify-between gap-6 px-4 py-3 ${!last ? "border-b border-[#2a2d35]" : ""}`}>
      <div className="flex-1 min-w-0">
        <p className="text-[#c8cad4] text-sm">{label}</p>
        {description && <p className="text-[#5a5f70] text-xs mt-0.5">{description}</p>}
      </div>
      <div className="shrink-0">{children}</div>
    </div>
  );
}

export function SettingsPage() {
  const [downloadPath, setDownloadPath] = useState("C:\\Users\\User\\Games\\wsteam");
  const [editingPath, setEditingPath] = useState(false);
  const [pathInput, setPathInput] = useState(downloadPath);

  const [maxSpeed, setMaxSpeed] = useState(0); // 0 = unlimited
  const [limitSpeed, setLimitSpeed] = useState(false);
  const [speedValue, setSpeedValue] = useState(50); // MB/s

  const [maxParallel, setMaxParallel] = useState(3);
  const [autoUpdate, setAutoUpdate] = useState(true);
  const [updateOnLaunch, setUpdateOnLaunch] = useState(false);
  const [updateSchedule, setUpdateSchedule] = useState("startup");

  const [verifyIntegrity, setVerifyIntegrity] = useState(true);
  const [deleteAfterInstall, setDeleteAfterInstall] = useState(true);
  const [cacheSize, setCacheSize] = useState(10);

  const [throttleBackground, setThrottleBackground] = useState(false);
  const [bandwidthBg, setBandwidthBg] = useState(10);

  const [notifications, setNotifications] = useState(true);
  const [notifyComplete, setNotifyComplete] = useState(true);
  const [notifyError, setNotifyError] = useState(true);
  const [notifyUpdate, setNotifyUpdate] = useState(false);

  const handleSavePath = () => {
    setDownloadPath(pathInput);
    setEditingPath(false);
  };

  return (
    <div className="flex-1 overflow-y-auto p-6 flex flex-col gap-6">
      {/* Download Location */}
      <Section title="Download Location">
        <div className="px-4 py-3">
          <p className="text-[#c8cad4] text-sm mb-2">Default install directory</p>
          {editingPath ? (
            <div className="flex gap-2">
              <input
                type="text"
                value={pathInput}
                onChange={(e) => setPathInput(e.target.value)}
                className="flex-1 bg-[#13151b] border border-[#4a9eff] text-[#c8cad4] px-3 py-2 rounded text-sm font-mono outline-none"
                autoFocus
              />
              <button onClick={handleSavePath} className="bg-[#4a9eff] text-white text-sm px-3 py-2 rounded hover:bg-[#3a8eef] transition-colors">
                Save
              </button>
              <button onClick={() => { setEditingPath(false); setPathInput(downloadPath); }} className="text-[#8a8f9e] text-sm px-3 py-2 rounded border border-[#2a2d35] hover:border-[#3a3d45] transition-colors">
                Cancel
              </button>
            </div>
          ) : (
            <div className="flex items-center gap-2">
              <div className="flex-1 bg-[#13151b] border border-[#2a2d35] rounded px-3 py-2 flex items-center gap-2">
                <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="#5a5f70" strokeWidth="2">
                  <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" />
                </svg>
                <span className="text-[#8a8f9e] text-sm font-mono truncate">{downloadPath}</span>
              </div>
              <button onClick={() => setEditingPath(true)} className="text-[#8a8f9e] text-sm px-3 py-2 rounded border border-[#2a2d35] hover:border-[#3a3d45] hover:text-[#c8cad4] transition-colors">
                Browse
              </button>
            </div>
          )}
          <p className="text-[#3a3d45] text-xs mt-2">Make sure the drive has enough free space for game installations.</p>
        </div>
      </Section>

      {/* Speed & Bandwidth */}
      <Section title="Speed & Bandwidth">
        <Row label="Limit download speed" description="Cap the maximum download speed">
          <Toggle checked={limitSpeed} onChange={setLimitSpeed} />
        </Row>
        <Row
          label="Max download speed"
          description={limitSpeed ? `${speedValue} MB/s` : "Unlimited"}
        >
          <div className="w-40 flex items-center gap-3">
            <Slider value={speedValue} min={1} max={200} step={1} onChange={setSpeedValue} disabled={!limitSpeed} />
          </div>
        </Row>
        <Row
          label="Parallel downloads"
          description={`Download up to ${maxParallel} file${maxParallel > 1 ? "s" : ""} simultaneously`}
        >
          <div className="flex items-center gap-2">
            <button
              onClick={() => setMaxParallel((v) => Math.max(1, v - 1))}
              className="w-7 h-7 rounded bg-[#13151b] border border-[#2a2d35] text-[#8a8f9e] hover:text-[#c8cad4] hover:border-[#3a3d45] transition-colors flex items-center justify-center"
            >
              −
            </button>
            <span className="text-[#c8cad4] text-sm w-4 text-center">{maxParallel}</span>
            <button
              onClick={() => setMaxParallel((v) => Math.min(8, v + 1))}
              className="w-7 h-7 rounded bg-[#13151b] border border-[#2a2d35] text-[#8a8f9e] hover:text-[#c8cad4] hover:border-[#3a3d45] transition-colors flex items-center justify-center"
            >
              +
            </button>
          </div>
        </Row>
        <Row label="Throttle when in background" description="Reduce speed when wsteam is not focused">
          <Toggle checked={throttleBackground} onChange={setThrottleBackground} />
        </Row>
        <Row
          label="Background bandwidth limit"
          description={throttleBackground ? `${bandwidthBg} MB/s` : "Disabled"}
          last
        >
          <div className="w-40 flex items-center gap-3">
            <Slider value={bandwidthBg} min={1} max={100} step={1} onChange={setBandwidthBg} disabled={!throttleBackground} />
          </div>
        </Row>
      </Section>

      {/* Updates */}
      <Section title="Updates">
        <Row label="Auto-update games" description="Automatically download game updates">
          <Toggle checked={autoUpdate} onChange={setAutoUpdate} />
        </Row>
        <Row label="Update on game launch" description="Check for updates when launching a game" >
          <Toggle checked={updateOnLaunch} onChange={setUpdateOnLaunch} disabled={!autoUpdate} />
        </Row>
        <Row label="Update schedule" description="When to run automatic updates" last>
          <select
            value={updateSchedule}
            onChange={(e) => setUpdateSchedule(e.target.value)}
            disabled={!autoUpdate}
            className="bg-[#13151b] border border-[#2a2d35] text-[#c8cad4] text-sm px-3 py-1.5 rounded outline-none focus:border-[#4a9eff] transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
          >
            <option value="startup">On startup</option>
            <option value="midnight">Daily at midnight</option>
            <option value="morning">Daily at 6 AM</option>
            <option value="manual">Manual only</option>
          </select>
        </Row>
      </Section>

      {/* Installation */}
      <Section title="Installation">
        <Row label="Verify file integrity" description="Check downloaded files for corruption before installing">
          <Toggle checked={verifyIntegrity} onChange={setVerifyIntegrity} />
        </Row>
        <Row label="Delete installer after install" description="Remove setup files after a successful installation">
          <Toggle checked={deleteAfterInstall} onChange={setDeleteAfterInstall} />
        </Row>
        <Row
          label="Download cache size"
          description={`Keep up to ${cacheSize} GB of cached files`}
          last
        >
          <div className="flex items-center gap-3">
            <div className="w-32">
              <Slider value={cacheSize} min={1} max={50} step={1} onChange={setCacheSize} />
            </div>
            <span className="text-[#8a8f9e] text-xs w-10 text-right">{cacheSize} GB</span>
          </div>
        </Row>
      </Section>

      {/* Notifications */}
      <Section title="Notifications">
        <Row label="Enable notifications" description="Show system notifications for download events">
          <Toggle checked={notifications} onChange={setNotifications} />
        </Row>
        <Row label="Download complete" description="Notify when a download finishes">
          <Toggle checked={notifyComplete} onChange={setNotifyComplete} disabled={!notifications} />
        </Row>
        <Row label="Download errors" description="Notify on download failures">
          <Toggle checked={notifyError} onChange={setNotifyError} disabled={!notifications} />
        </Row>
        <Row label="Update available" description="Notify when game updates are found" last>
          <Toggle checked={notifyUpdate} onChange={setNotifyUpdate} disabled={!notifications} />
        </Row>
      </Section>

      {/* Danger Zone */}
      <Section title="Data">
        <div className="p-4 flex flex-col gap-3">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-[#c8cad4] text-sm">Clear download cache</p>
              <p className="text-[#5a5f70] text-xs mt-0.5">Remove cached download data to free up space</p>
            </div>
            <button className="text-sm text-[#8a8f9e] px-3 py-1.5 rounded border border-[#2a2d35] hover:border-[#3a3d45] hover:text-[#c8cad4] transition-colors">
              Clear Cache
            </button>
          </div>
          <div className="h-px bg-[#2a2d35]" />
          <div className="flex items-center justify-between">
            <div>
              <p className="text-[#c8cad4] text-sm">Reset all settings</p>
              <p className="text-[#5a5f70] text-xs mt-0.5">Restore all download settings to their defaults</p>
            </div>
            <button className="text-sm text-[#ff5f5f] px-3 py-1.5 rounded border border-[#ff5f5f]/30 hover:bg-[#ff5f5f]/10 transition-colors">
              Reset Settings
            </button>
          </div>
        </div>
      </Section>

      <div className="pb-2" />
    </div>
  );
}

import { Flyout } from "./pages/Flyout";
import { Settings } from "./pages/Settings";

function windowKind(): "flyout" | "settings" {
  const q = new URLSearchParams(window.location.search).get("window");
  if (q === "settings") return "settings";
  return "flyout";
}

export default function App() {
  return windowKind() === "settings" ? <Settings /> : <Flyout />;
}

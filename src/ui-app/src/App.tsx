import { Routes, Route, Navigate } from 'react-router-dom';
import LandingScene from './scenes/Landing/LandingScene';
import TradeDeskScene from './scenes/TradeDesk/TradeDeskScene';
import TdMorningBriefScene from './scenes/TradeDesk/TdMorningBriefScene';
import WorkspaceScene from './scenes/Workspace/WorkspaceScene';
import RmBriefingScene from './scenes/RmBriefing/RmBriefingScene';
import MorningBriefScene from './scenes/MorningBrief/MorningBriefScene';
import AdminScene from './scenes/Admin/AdminScene';
import CockpitScene from './scenes/Cockpit/CockpitScene';
import ChatScene from './scenes/Chat/ChatScene';

export default function App() {
  return (
    <Routes>
      {/* Landing chooser picks a workspace. Institutional Sales & Trading (/desk) is the
          demo focus; the Commercial Banking RM workspace (/cb) remains fully available. */}
      <Route path="/" element={<LandingScene />} />

      {/* Institutional Sales & Trading */}
      <Route path="/desk" element={<TradeDeskScene />} />
      <Route path="/desk/morning-brief" element={<TdMorningBriefScene />} />

      {/* Commercial Banking RM */}
      <Route path="/cb" element={<WorkspaceScene />} />
      <Route path="/rm-briefing" element={<RmBriefingScene />} />
      <Route path="/morning-brief" element={<MorningBriefScene />} />
      <Route path="/cockpit" element={<CockpitScene />} />

      {/* Shared surfaces */}
      <Route path="/chat" element={<ChatScene />} />
      <Route path="/admin" element={<AdminScene />} />

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

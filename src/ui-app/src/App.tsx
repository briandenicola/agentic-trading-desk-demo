import { Routes, Route, Navigate } from 'react-router-dom';
import WorkspaceScene from './scenes/Workspace/WorkspaceScene';
import RmBriefingScene from './scenes/RmBriefing/RmBriefingScene';
import MorningBriefScene from './scenes/MorningBrief/MorningBriefScene';
import AdminScene from './scenes/Admin/AdminScene';
import CockpitScene from './scenes/Cockpit/CockpitScene';
import ChatScene from './scenes/Chat/ChatScene';

export default function App() {
  return (
    <Routes>
      {/* M.INT workspace shell is the PRIMARY screen. RM Daily Briefing and the
          trading morning brief remain available on their own routes. */}
      <Route path="/" element={<WorkspaceScene />} />
      <Route path="/rm-briefing" element={<RmBriefingScene />} />
      <Route path="/morning-brief" element={<MorningBriefScene />} />
      <Route path="/cockpit" element={<CockpitScene />} />
      <Route path="/chat" element={<ChatScene />} />
      <Route path="/admin" element={<AdminScene />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

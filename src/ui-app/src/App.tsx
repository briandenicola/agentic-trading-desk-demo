import { Routes, Route, Navigate } from 'react-router-dom';
import RmBriefingScene from './scenes/RmBriefing/RmBriefingScene';
import MorningBriefScene from './scenes/MorningBrief/MorningBriefScene';
import AdminScene from './scenes/Admin/AdminScene';
import CockpitScene from './scenes/Cockpit/CockpitScene';
import ChatScene from './scenes/Chat/ChatScene';

export default function App() {
  return (
    <Routes>
      {/* RM Daily Briefing is the PRIMARY scene; trading morning brief is secondary. */}
      <Route path="/" element={<RmBriefingScene />} />
      <Route path="/rm-briefing" element={<RmBriefingScene />} />
      <Route path="/morning-brief" element={<MorningBriefScene />} />
      <Route path="/cockpit" element={<CockpitScene />} />
      <Route path="/chat" element={<ChatScene />} />
      <Route path="/admin" element={<AdminScene />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

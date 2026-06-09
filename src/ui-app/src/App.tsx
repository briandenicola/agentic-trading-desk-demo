import { Routes, Route, Navigate } from 'react-router-dom';
import RmBriefingScene from './scenes/RmBriefing/RmBriefingScene';
import MorningBriefScene from './scenes/MorningBrief/MorningBriefScene';

export default function App() {
  return (
    <Routes>
      {/* RM Daily Briefing is the PRIMARY scene; trading morning brief is secondary. */}
      <Route path="/" element={<RmBriefingScene />} />
      <Route path="/rm-briefing" element={<RmBriefingScene />} />
      <Route path="/morning-brief" element={<MorningBriefScene />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

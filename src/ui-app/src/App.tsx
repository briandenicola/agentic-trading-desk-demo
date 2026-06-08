import { Routes, Route, Navigate } from 'react-router-dom';
import MorningBriefScene from './scenes/MorningBrief/MorningBriefScene';

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<MorningBriefScene />} />
      <Route path="/morning-brief" element={<MorningBriefScene />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

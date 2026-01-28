import React, { useState } from 'react';
import LandingPage from './LandingPage';

export default function Application() {
  const [showLanding] = useState(true);

  return (
    <div>
      <LandingPage visible={showLanding} />
    </div>
  );
}

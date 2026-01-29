import React from 'react';

export default function AnswerPage({ visible = true, message = '' }) {
  if (!visible) return null;

  return (
    <div>
      <h1>Health Test Questionnaire</h1>
      <div>
        <p>{message}</p>
      </div>
    </div>
  );
}

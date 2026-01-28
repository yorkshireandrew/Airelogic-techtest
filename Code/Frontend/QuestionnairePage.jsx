import React, { useEffect, useState } from 'react';

export default function QuestionnairePage({ visible = true, ageBand: ageBandProp }) {
  const [questions, setQuestions] = useState([]);
  const [selectedAnswers, setSelectedAnswers] = useState(new Set());
  const [ageBand, setAgeBand] = useState(ageBandProp || '');

  useEffect(() => {
    // Fetch questions from API endpoint
    let cancelled = false;
    async function load() {
      try {
        const res = await fetch('/api/questions');
        if (!res.ok) throw new Error('Failed to fetch');
        const body = await res.json();
        if (cancelled) return;
        const qs = Array.isArray(body.questions) ? body.questions : (body || []);
        setQuestions(qs);
        setAgeBand(body.ageBand || ageBandProp || '');
        setSelectedAnswers(new Set());
      } catch (e) {
        // on error, treat as no questions
        if (!cancelled) {
          setQuestions([]);
          setSelectedAnswers(new Set());
        }
      }
    }
    load();
    return () => { cancelled = true; };
  }, [ageBandProp]);

  if (!visible) return null;

  const totalQuestions = questions.length;

  const onAnswerChange = (name) => {
    // name is like 'Y0' or 'N0'
    setSelectedAnswers(prev => {
      const copy = new Set(prev);
      // determine other name (Y<index> <-> N<index>)
      const prefix = name.charAt(0);
      const number = name.substring(1);
      const otherName = (prefix === 'Y' ? 'N' : 'Y') + number;
      // toggle selected state for this name
      if (copy.has(name)) {
        copy.delete(name);
      } else {
        copy.add(name);
        // remove opposing selection
        if (copy.has(otherName)) copy.delete(otherName);
      }
      return copy;
    });
  };

  const isSubmitEnabled = selectedAnswers.size === totalQuestions;

  return (
    <div>
      <h1>Health Test Questionnaire</h1>
      <form id="health-form" method="post" action="/questionnaire-submit">
        <table>
          <thead>
            <tr>
              <th>Question</th>
              <th>Yes</th>
              <th>No</th>
            </tr>
          </thead>
          <tbody>
            {questions.map((q, i) => (
              <tr key={i}>
                <td>{q}</td>
                <td>
                  <input
                    type="radio"
                    name={`Y${i}`}
                    onChange={() => onAnswerChange(`Y${i}`)}
                    checked={selectedAnswers.has(`Y${i}`)}
                  />
                </td>
                <td>
                  <input
                    type="radio"
                    name={`N${i}`}
                    onChange={() => onAnswerChange(`N${i}`)}
                    checked={selectedAnswers.has(`N${i}`)}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        <input type="hidden" name="AgeBand" value={ageBand} />
        <input type="hidden" name="TotalQuestions" value={totalQuestions} />
        <button id="health-submit" type="submit" disabled={!isSubmitEnabled}>Submit</button>
      </form>
    </div>
  );
}

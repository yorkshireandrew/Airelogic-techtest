const { useState } = React;

function Application() {
  const [showLanding, setShowLanding] = useState(true);
  const [showAnswer, setShowAnswer] = useState(false);
  const [answerMessage, setAnswerMessage] = useState('');
  const [showQuestionnaire, setShowQuestionnaire] = useState(false);
  const [questionnaireAgeBand, setQuestionnaireAgeBand] = useState('');

  const handleLandingResponse = (resp) => {
    if (!resp) return;
    // Server JSON uses camelCase keys (e.g. "message" and "ageBand").
    // If message present -> show AnswerPage. If message is empty string -> show Questionnaire.
    if (resp.message && resp.message !== '') {
      setAnswerMessage(resp.message);
      setShowAnswer(true);
      setShowLanding(false);
    } else if (resp.message === '') {
      // show questionnaire and pass ageBand from response (camelCase)
      setQuestionnaireAgeBand(resp.ageBand || '');
      setShowQuestionnaire(true);
      setShowLanding(false);
    }
  };

  return (
    <div>
      <LandingPage visible={showLanding} onSubmitResponse={handleLandingResponse} />
      <AnswerPage visible={showAnswer} message={answerMessage} />
      <QuestionnairePage visible={showQuestionnaire} ageBand={questionnaireAgeBand} />
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<Application />);

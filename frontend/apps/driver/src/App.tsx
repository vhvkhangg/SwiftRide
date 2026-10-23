import TripShell from '../../../shared/TripShell';
import Driver from './Driver';
import { screens } from './screens';

export default function App() {
  return <TripShell role="driver" screens={screens} renderScreen={props => <Driver key={`${props.state.status}-${props.state.tripNumber}`} {...props} />} />;
}

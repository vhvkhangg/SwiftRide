import TripShell from '../../../shared/TripShell';
import Rider from './Rider';
import { screens } from './screens';

export default function App() {
  return <TripShell role="rider" screens={screens} renderScreen={props => <Rider key={`${props.state.status}-${props.state.tripNumber}`} {...props} />} />;
}

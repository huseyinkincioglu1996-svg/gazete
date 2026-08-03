import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { getParentMenu } from '../navigation';
import BackIcon from './BackIcon';
import './EdgeSwipeBack.css';

const EDGE_START_PX = 34;
const MAX_VERTICAL_DRIFT_PX = 72;

const EMPTY_GESTURE = Object.freeze({
  active: false,
  progress: 0,
  ready: false,
});

const getActivationDistance = () => Math.min(
  60,
  Math.max(44, window.innerWidth * 0.125),
);

const getProgress = (distance, activationDistance) => Math.min(
  Math.max(distance / activationDistance, 0),
  1,
);

function EdgeSwipeBack() {
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const backTarget = useMemo(() => getParentMenu(pathname), [pathname]);
  const pointerRef = useRef({
    pointerId: null,
    startX: 0,
    startY: 0,
    tracking: false,
  });
  const suppressClickRef = useRef(false);
  const suppressClickTimerRef = useRef(null);
  const [gesture, setGesture] = useState(EMPTY_GESTURE);

  useEffect(() => {
    const clearPointerState = () => {
      pointerRef.current = {
        pointerId: null,
        startX: 0,
        startY: 0,
        tracking: false,
      };
      document.body.classList.remove('edge-swipe-active');
      setGesture(EMPTY_GESTURE);
    };

    if (!backTarget) {
      clearPointerState();
      return undefined;
    }

    const handlePointerDown = (event) => {
      const isPrimaryMouseButton = event.pointerType !== 'mouse' || event.button === 0;
      const hasPrimaryMouseButtonPressed = event.pointerType !== 'mouse' || event.buttons === 1;

      if (
        !event.isPrimary
        || !isPrimaryMouseButton
        || !hasPrimaryMouseButtonPressed
        || event.clientX > EDGE_START_PX
      ) {
        return;
      }

      pointerRef.current = {
        pointerId: event.pointerId,
        startX: event.clientX,
        startY: event.clientY,
        tracking: true,
      };
      setGesture({
        active: true,
        progress: 0,
        ready: false,
      });
    };

    const handlePointerMove = (event) => {
      const pointer = pointerRef.current;

      if (!pointer.tracking || event.pointerId !== pointer.pointerId) {
        return;
      }

      const distanceX = event.clientX - pointer.startX;
      const distanceY = event.clientY - pointer.startY;
      const verticalDrift = Math.abs(distanceY);

      if (verticalDrift > MAX_VERTICAL_DRIFT_PX && verticalDrift > Math.abs(distanceX)) {
        clearPointerState();
        return;
      }

      const isHorizontalDrag = distanceX > 8 && distanceX > verticalDrift;
      const activationDistance = getActivationDistance();
      const progress = getProgress(distanceX, activationDistance);

      if (isHorizontalDrag) {
        event.preventDefault();
        document.body.classList.add('edge-swipe-active');
      }

      setGesture({
        active: true,
        progress,
        ready: distanceX >= activationDistance
          && verticalDrift <= MAX_VERTICAL_DRIFT_PX
          && distanceX > verticalDrift * 1.25,
      });
    };

    const handlePointerEnd = (event) => {
      const pointer = pointerRef.current;

      if (!pointer.tracking || event.pointerId !== pointer.pointerId) {
        return;
      }

      const distanceX = event.clientX - pointer.startX;
      const verticalDrift = Math.abs(event.clientY - pointer.startY);
      const shouldNavigate = distanceX >= getActivationDistance()
        && verticalDrift <= MAX_VERTICAL_DRIFT_PX
        && distanceX > verticalDrift * 1.25;

      if (shouldNavigate) {
        event.preventDefault();
        suppressClickRef.current = true;
        window.clearTimeout(suppressClickTimerRef.current);
        suppressClickTimerRef.current = window.setTimeout(() => {
          suppressClickRef.current = false;
        }, 350);
      }

      clearPointerState();

      if (shouldNavigate) {
        navigate(backTarget, { replace: true });
      }
    };

    const handleClick = (event) => {
      if (!suppressClickRef.current) {
        return;
      }

      suppressClickRef.current = false;
      window.clearTimeout(suppressClickTimerRef.current);
      event.preventDefault();
      event.stopPropagation();
    };

    window.addEventListener('pointerdown', handlePointerDown, true);
    window.addEventListener('pointermove', handlePointerMove, { capture: true, passive: false });
    window.addEventListener('pointerup', handlePointerEnd, true);
    window.addEventListener('pointercancel', clearPointerState, true);
    window.addEventListener('blur', clearPointerState);
    window.addEventListener('click', handleClick, true);

    return () => {
      window.removeEventListener('pointerdown', handlePointerDown, true);
      window.removeEventListener('pointermove', handlePointerMove, true);
      window.removeEventListener('pointerup', handlePointerEnd, true);
      window.removeEventListener('pointercancel', clearPointerState, true);
      window.removeEventListener('blur', clearPointerState);
      window.removeEventListener('click', handleClick, true);
      window.clearTimeout(suppressClickTimerRef.current);
      document.body.classList.remove('edge-swipe-active');
    };
  }, [backTarget, navigate]);

  if (!backTarget) {
    return null;
  }

  return (
    <div
      className={`edge-swipe-indicator${gesture.active ? ' active' : ''}${gesture.ready ? ' ready' : ''}`}
      data-testid="edge-swipe-indicator"
      style={{ '--edge-swipe-progress': gesture.progress }}
      aria-hidden="true"
    >
      <BackIcon className="edge-swipe-icon" />
      <span>{gesture.ready ? 'BIRAK' : 'GERİ'}</span>
    </div>
  );
}

export default EdgeSwipeBack;

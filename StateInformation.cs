using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace LockInitClient
{
    public enum InitializationState
    {
        Assigning = 2,
        InitializeConfig = 3,
        TestingConfig = 4,
        Done = 5,
        MaxState = 6
    }

    public class StateInformation
    {
        public StateInformation()
        {
            CurrentState = InitializationState.Assigning; 
        }

        public IPEndPoint DeviceEndpoint { get; set; }
        public InitializationState CurrentState { get; set; }
        public string DeviceId { get; set; }

        public void ResetState()
        {
            CurrentState = InitializationState.Assigning;
        }

        public InitializationState NextState(int deviceStateVal)
        {
            if (deviceStateVal >= (int)InitializationState.MaxState || deviceStateVal < 1)
                return InitializationState.Assigning;

            InitializationState deviceState = (InitializationState) deviceStateVal;
            InitializationState nextState = InitializationState.Assigning;

            switch (deviceState)
            {
                case InitializationState.Assigning:
                    if (CurrentState == InitializationState.Assigning)
                    {
                        nextState = InitializationState.InitializeConfig;
                    }                   
                    break;

                case InitializationState.InitializeConfig:
                    if (CurrentState == InitializationState.InitializeConfig)
                    {
                        nextState = InitializationState.TestingConfig;
                    }
                    break;

                case InitializationState.TestingConfig:
                    break;

                default:
                    break;
            }

            return nextState;
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.QnA;
using Microsoft.Bot.Builder.AI.QnA.Dialogs;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using EchoBot.OmniChannel;

namespace Microsoft.BotBuilderSamples.Bots
{
    public class QnABot<T> : ActivityHandler where T : Microsoft.Bot.Builder.Dialogs.Dialog
    {
        protected readonly BotState ConversationState;
        protected readonly Microsoft.Bot.Builder.Dialogs.Dialog Dialog;
        protected readonly BotState UserState;
        private readonly IConfiguration _configuration;

        public QnABot(ConversationState conversationState, UserState userState, T dialog, IConfiguration configuration)
        {
            ConversationState = conversationState;
            UserState = userState;
            Dialog = dialog;

            _configuration = configuration;
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            await base.OnTurnAsync(turnContext, cancellationToken);

            // Save any state changes that might have occured during the turn.
            await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            try
            {
                var httpClient = new HttpClient();

                var qnaMaker = new QnAMaker(new QnAMakerEndpoint
                {
                    KnowledgeBaseId = "461a9694-ee84-4423-b283-600e2e978b0c", //_configuration["QnAKnowledgebaseId"],
                    EndpointKey = "39b9495c-b1a8-4a87-ac23-ad566a40dd80", //_configuration["QnAEndpointKey"],
                    Host = GetHostname("mooqna.azurewebsites.net")//_configuration["QnAEndpointHostName"])
                },
                null,
                httpClient);

                var options = new QnAMakerOptions { Top = 1 };

                // The actual call to the QnA Maker service.
                var response = await qnaMaker.GetAnswersAsync(turnContext, options);

                IActivity replyActivity = MessageFactory.Text($"{response[0].Answer}");

                // Replace with your own condition for bot escalation
                if (turnContext.Activity.Text.Equals("escalate", StringComparison.InvariantCultureIgnoreCase))
                {
                    Dictionary<string, object> contextVars = new Dictionary<string, object>() { { "BotHandoffTopic", "CreditCard" } };
                    OmnichannelBotClient.AddEscalationContext(replyActivity, contextVars);
                }
                // Replace with your own condition for bot end conversation
                else if (turnContext.Activity.Text.Equals("endconversation", StringComparison.InvariantCultureIgnoreCase))
                {
                    OmnichannelBotClient.AddEndConversationContext(replyActivity);
                }
                // Call method BridgeBotMessage for every response that needs to be delivered to the customer.
                else
                {
                    OmnichannelBotClient.BridgeBotMessage(replyActivity);
                }

                await turnContext.SendActivityAsync(replyActivity, cancellationToken);

            }

            catch (Exception ex)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(ex.ToString()), cancellationToken);
            }
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Hello and welcome!"), cancellationToken);
                }
            }
        }
        private static string GetHostname(string hostname)
        {
            if (!hostname.StartsWith("https://"))
            {
                hostname = string.Concat("https://", hostname);
            }

            if (!hostname.EndsWith("/qnamaker"))
            {
                hostname = string.Concat(hostname, "/qnamaker");
            }

            return hostname;
        }
    }
}

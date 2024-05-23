using System;

namespace InfraSampleWebApp;

public class BaseInfraStack : Stack
{
    public BaseStackProps BaseStackProps { get; private set; }

    internal BaseInfraStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
    {
        BaseStackProps = new()
        {
            Vpc = new Vpc(this, "demo-vpc", new VpcProps
            {
                IpAddresses = IpAddresses.Cidr("172.30.0.0/16"),
                MaxAzs = 3
            })
        };

        // ECS Cluster
        BaseStackProps.Cluster = new Cluster(this, "demo-cluster", new ClusterProps
        {
            Vpc = BaseStackProps.Vpc,
            ContainerInsights = true
        });

        BaseStackProps.Topic = new Topic(this, "Topic", new TopicProps
        {
            DisplayName = "Customer subscription topic",
            TopicName = "demo-web-app-topic"
        });

        BaseStackProps.LogGroup = new LogGroup(this, "demo-log-group", new LogGroupProps
        {
            LogGroupName = BaseStackProps.LogGroupName,
            Retention = RetentionDays.ONE_DAY,
            RemovalPolicy = BaseStackProps.CleanUpRemovePolicy,
        });
    }
}
